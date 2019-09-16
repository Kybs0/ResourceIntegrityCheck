using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Path = System.IO.Path;

namespace ResourceIntegrityCheck
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private List<string> ResourceFilterKeys = new List<string>() { ".JiaoPei.xaml", "_Training.xaml" };
        private void CheckButton_OnClick(object sender, RoutedEventArgs e)
        {
            var projectFolder = ProjectFolderTextBox.Text;
            if (!Directory.Exists(projectFolder))
            {
                return;
            }

            IsSearching = true;
            try
            {
                ErrorResourceInfos = null;
                ErrorResourceInfos = SearchErrorResource(projectFolder);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
            finally
            {
                IsSearching = false;
            }
        }

        private List<ErrorResourceInfo> SearchErrorResource(string projectFolder)
        {
            var errorInfos = new List<ErrorResourceInfo>();
            //获取所有匹配的xaml文件
            var searchedFileInfos = GetSearchedFileInfos(projectFolder);
            foreach (var searchFileInfo in searchedFileInfos)
            {
                //获取所有资源
                List<SingleResource> totalResources = new List<SingleResource>();
                var specialVersionResources = GetAllKeysFromFile(searchFileInfo.specialVersionPath);
                totalResources.AddRange(specialVersionResources);
                var normalVersionResources = GetAllKeysFromFile(searchFileInfo.normalPath);
                foreach (var normalVersionResource in normalVersionResources)
                {
                    if (totalResources.All(i => i.Key != normalVersionResource.Key))
                    {
                        totalResources.Add(normalVersionResource);
                    }
                }
                //筛选出缺失的资源
                foreach (var singleResource in totalResources)
                {
                    if (specialVersionResources.All(i => i.Key != singleResource.Key))
                    {
                        var errorResourceInfo = new ErrorResourceInfo()
                        {
                            ResourceDictionaryFileName = Path.GetFileName(searchFileInfo.specialVersionPath),
                            ResourceDictionaryFilePath = searchFileInfo.specialVersionPath,
                            ErrorText = $"缺失资源：{singleResource.Key}",
                            SingleResource = singleResource
                        };
                        errorInfos.Add(errorResourceInfo);
                    }
                    if (normalVersionResources.All(i => i.Key != singleResource.Key))
                    {
                        var errorResourceInfo = new ErrorResourceInfo()
                        {
                            ResourceDictionaryFileName = Path.GetFileName(searchFileInfo.normalPath),
                            ResourceDictionaryFilePath = searchFileInfo.normalPath,
                            ErrorText = $"缺失资源：{singleResource.Key}",
                            SingleResource = singleResource
                        };
                        errorInfos.Add(errorResourceInfo);
                    }
                }
            }

            return errorInfos;
        }

        private const string KeyConstString = "x:Key=\"";
        /// <summary>
        /// 从文件获取所有资源值
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private IEnumerable<SingleResource> GetAllKeysFromFile(string filePath)
        {
            var result = new List<SingleResource>();
            var list = File.ReadAllLines(filePath).ToList();

            SingleResource singleResource = null;
            foreach (var textLine in list)
            {
                if (textLine.Contains(KeyConstString))
                {
                    var startIndex = textLine.IndexOf(KeyConstString) + KeyConstString.Length;
                    var endIndex = textLine.IndexOf("\"", startIndex);
                    var keyString = textLine.Substring(startIndex, endIndex - startIndex);

                    singleResource = new SingleResource();
                    singleResource.Key = keyString;
                    singleResource.LineIndex = list.IndexOf(textLine);
                    singleResource.ResourceLines.Add(textLine);
                    result.Add(singleResource);
                }
                else
                {
                    singleResource?.ResourceLines.Add(textLine);
                }
            }
            return result;
        }
        /// <summary>
        /// 从项目中获取所有匹配的文件项
        /// </summary>
        /// <param name="projectFolder"></param>
        /// <returns></returns>
        private List<(string normalPath, string specialVersionPath)> GetSearchedFileInfos(string projectFolder)
        {
            var valueTuples = new List<(string fileInfo, string fileInfo1)>();
            var projectDirectoryInfo = new DirectoryInfo(projectFolder);
            var specialVersionFiles = projectDirectoryInfo.GetFiles("*.xaml").Where(file => ResourceFilterKeys.Any(pattern => file.FullName.Contains(pattern)));
            foreach (var fileInfo in specialVersionFiles)
            {
                var specialVersionPath = fileInfo.FullName;
                var normalPath = specialVersionPath;

                ResourceFilterKeys.ForEach(filterKey => normalPath = normalPath.Replace(filterKey, ".xaml"));
                valueTuples.Add((normalPath, specialVersionPath));
            }

            var directoryInfos = projectDirectoryInfo.GetDirectories();
            foreach (var directoryInfo in directoryInfos)
            {
                valueTuples.AddRange(GetSearchedFileInfos(directoryInfo.FullName));
            }
            return valueTuples;
        }
        /// <summary>
        /// 链接地址
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ErrorResourceLinkButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ErrorResourceInfo resourceInfo)
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select," + resourceInfo.ResourceDictionaryFilePath);
            }
        }

        private void FixButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (ErrorListBox.ItemsSource is List<ErrorResourceInfo> errorResourceInfos)
            {
                foreach (var errorResourceInfo in errorResourceInfos)
                {
                    //获取所有行数据
                    var allLines = File.ReadAllLines(errorResourceInfo.ResourceDictionaryFilePath).ToList();
                    var lastLine = allLines[allLines.Count - 1];
                    allLines.RemoveAt(allLines.Count - 1);
                    //先写入之前数据（除末尾）
                    File.WriteAllLines(errorResourceInfo.ResourceDictionaryFilePath, allLines);
                    //写入新数据
                    File.AppendAllLines(errorResourceInfo.ResourceDictionaryFilePath, errorResourceInfo.SingleResource.ResourceLines);
                    //写入之前末尾数据
                    File.AppendAllLines(errorResourceInfo.ResourceDictionaryFilePath, new List<string>() { lastLine });
                }

                ErrorListBox.ItemsSource = null;
                MessageBox.Show("修复成功！");
            }
        }

        public static readonly DependencyProperty ErrorResourceInfosProperty = DependencyProperty.Register(
            "ErrorResourceInfos", typeof(List<ErrorResourceInfo>), typeof(MainWindow), new PropertyMetadata(default(List<ErrorResourceInfo>)));

        public List<ErrorResourceInfo> ErrorResourceInfos
        {
            get { return (List<ErrorResourceInfo>)GetValue(ErrorResourceInfosProperty); }
            set { SetValue(ErrorResourceInfosProperty, value); }
        }

        public static readonly DependencyProperty IsSearchingProperty = DependencyProperty.Register(
            "IsSearching", typeof(bool), typeof(MainWindow), new PropertyMetadata(default(bool)));

        public bool IsSearching
        {
            get { return (bool)GetValue(IsSearchingProperty); }
            set { SetValue(IsSearchingProperty, value); }
        }
    }

    public class ErrorResourceInfo
    {
        public string ResourceDictionaryFileName { get; set; }
        public string ErrorText { get; set; }
        public SingleResource SingleResource { get; set; }
        public string ResourceDictionaryFilePath { get; set; }
    }

    public class SingleResource
    {
        public int LineIndex { get; set; }
        public string Key { get; set; }
        public List<string> ResourceLines { get; set; } = new List<string>();
    }
}
