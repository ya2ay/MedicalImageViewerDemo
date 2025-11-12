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

                    _currentSeries = DicomSeries.LoadFromFolder(folder);

                    // 分配数据
                    Host3D.SetDicomSeries(_currentSeries, DirectXRenderer.RenderMode.Volume3D);
                    HostAxial.SetDicomSeries(_currentSeries, DirectXRenderer.RenderMode.AxialSlice);
                    HostCoronal.SetDicomSeries(_currentSeries, DirectXRenderer.RenderMode.CoronalSlice);
                    HostSagittal.SetDicomSeries(_currentSeries, DirectXRenderer.RenderMode.SagittalSlice);
                }
                catch (Exception ex)
                {
                    WinMessageBox.Show("Error: " + ex.Message);
                }
            }
        }
    }
}

    


