using dnlib.DotNet;
using dnlib.DotNet.Writer;
using Microsoft.Win32;
using ProxyCall_Remover.Deobfuscation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace ProxyCall_Remover
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            _instance = this;

            InitializeComponent();
        }

        private string _path;

        private ModuleDef _module;

        private readonly List<IDeobfuscator> Deobfuscators = new List<IDeobfuscator>()
        {
            new ProxyCallRemover()
        };

        internal static MainWindow _instance;

        internal static void Output(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            TextBlock textBlock = new TextBlock()
            {
                VerticalAlignment = VerticalAlignment.Top,
                Text = text
            };

            _instance.StackPanelOutput.Children.Add(textBlock);
            _instance.StackPanelOutput.UpdateLayout();
            //_instance.TextBoxOutput.Scrol
            //_instance.TextBoxOutput.Text += ;
        }

        private void ButtonSelectPath_Click(object sender, RoutedEventArgs e)
        {
            if (ShowDialogPath() == false)
                return;
        }

        private void ButtonUnpack_Click(object sender, RoutedEventArgs e)
        {
            if (CheckModule() == false)
                return;

            DoDeobfuscate();

            SaveModule();
        }

        private void DoDeobfuscate()
        {
            foreach (var deobfuscator in Deobfuscators)
            {
                Output("Step: " + deobfuscator.Name);
                deobfuscator.RemoveProtection(_module);

                Output("Removed proxy calls: " + deobfuscator.GetResult());

                deobfuscator.Dispose();
                //MessageBox.Show(deobfuscator.GetResult().ToString());
            }
        }

        private void SaveModule()
        {
            string output_path = string.Empty;

            if (GetOutputPath(ref output_path) == false)
                return;

            DoSaveModule(output_path);
        }

        private void DoSaveModule(string path)
        {
            try
            {
                _module.Write(path);
            }
            catch (ModuleWriterException)
            {
                _module.Write(path, new ModuleWriterOptions(_module)
                {
                    Logger = DummyLogger.NoThrowInstance
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error");
            }
        }

        private bool CheckModule()
        {
            try
            {
                _module = ModuleDefMD.Load(_path);
            }
            catch (BadImageFormatException)
            {
                MessageBox.Show("File is not valid");
                return false;
            }

            return true;
        }

        private bool ShowDialogPath()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Assembly (*.exe;*.dll)|*.exe;*.dll",
                CheckFileExists = true,
                Multiselect = false
            };
            if (openFileDialog.ShowDialog() == true)
            {
                if (File.Exists(openFileDialog.FileName))
                {
                    _path = openFileDialog.FileName;
                    textBoxPath.Text = _path;
                    return true;
                }
                else
                {
                    MessageBox.Show("The file does not exist");
                }
            }
            return false;
        }

        private bool GetOutputPath(ref string output_path)
        {
            string path = System.IO.Path.GetDirectoryName(_path);
            try
            {
                path += "\\Unpacked";

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                path += "\\" + Path.GetFileNameWithoutExtension(_path) + "_unpacked" + Path.GetExtension(_path);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("UnauthorizedAccessException", "Error");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error");
            }

            output_path = path;

            return true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files.Length < 1)
                    return;

                string file = files[0];

                string ext = Path.GetExtension(file).ToLower();

                switch (ext)
                {
                    case ".exe":
                    case ".dll":
                        _path = file;
                        textBoxPath.Text = file;
                        e.Effects = DragDropEffects.Move;
                        return;

                    default:
                        e.Effects = DragDropEffects.None;
                        break;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            OptionsUnpack.IsEnabledRemoveJunks = !OptionsUnpack.IsEnabledRemoveJunks;
        }
    }
}