//using System.Text;
//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Data;
//using System.Windows.Documents;
//using System.Windows.Input;
//using System.Windows.Media;
//using System.Windows.Media.Imaging;
//using System.Windows.Navigation;
//using System.Windows.Shapes;
//using Microsoft.Win32;
using FellowOakDicom;
using System.IO;
using System.Windows;
using WinOpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WinMessageBox = System.Windows.MessageBox;
using IOPath = System.IO.Path;
using FellowOakDicom.Log;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using System.Text;


namespace MedicalRenderDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DicomSeries? _currentSeries; // 注意：DicomSeries 是你自定义的类

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void OnLoadClick(object sender, RoutedEventArgs e)
        {
            var fileDialog = new WinOpenFileDialog();
            fileDialog.Filter = "DICOM files (*.dcm)|*.dcm";
            if (fileDialog.ShowDialog() == true)
            {
                string folder = IOPath.GetDirectoryName(fileDialog.FileName)!;
                try
                {
                    var files = Directory.GetFiles(folder, "*.dcm");
                    var first = DicomFile.Open(files[0]);
                    int rows = first.Dataset.GetValue<int>(DicomTag.Rows, 0);
                    int cols = first.Dataset.GetValue<int>(DicomTag.Columns, 0);
                    WinMessageBox.Show($"Loaded {files.Length} slices, size: {cols}x{rows}");

                    _currentSeries = LoadFromFolder(folder);

                    // 分配数据
                    HostAxial.SetDicomSeries(_currentSeries, DirectXRenderer.RenderMode.AxialSlice);
                    HostCoronal.SetDicomSeries(_currentSeries, DirectXRenderer.RenderMode.CoronalSlice);
                    HostSagittal.SetDicomSeries(_currentSeries, DirectXRenderer.RenderMode.SagittalSlice);
                    Host3D.SetDicomSeries(_currentSeries, DirectXRenderer.RenderMode.Volume3D);
                }
                catch (Exception ex)
                {
                    WinMessageBox.Show("Error: " + ex.Message);
                }
            }
        }

        public DicomSeries LoadFromFolder(string folderPath)
        {
            var series = new DicomSeries();
            var files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                                 .Where(f => IsDicomFile(f))
                                 .Select(f => DicomFile.Open(f))
                                 .ToList();

            if (files.Count == 0) throw new ArgumentException("No DICOM files found.");

            // 按 ImagePositionPatient 排序（Z 轴）
            files.Sort((a, b) =>
            {
                var posA = a.Dataset.GetValues<double>(DicomTag.ImagePositionPatient);
                var posB = b.Dataset.GetValues<double>(DicomTag.ImagePositionPatient);
                return posA[2].CompareTo(posB[2]); // Z 坐标
            });

            series.Files.AddRange(files);
            return series;
        }

        private static bool IsDicomFile(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                var buffer = new byte[128 + 4];
                stream.Read(buffer, 0, buffer.Length);
                return Encoding.ASCII.GetString(buffer, 128, 4) == "DICM";
            }
            catch { return false; }
        }
    }
}

    


