using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
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
using System.Windows.Shapes;

using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using System.Drawing;


namespace panoramaOnvifVideo
{
    class IniFile   // revision 11
    {
        string Path;
        string EXE = Assembly.GetExecutingAssembly().GetName().Name;

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern long WritePrivateProfileString(string Section, string Key, string Value, string FilePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

        public IniFile(string IniPath = null)
        {
            Path = new FileInfo(IniPath ?? EXE + ".ini").FullName.ToString();
        }

        public string Read(string Key, string Section = null)
        {
            var RetVal = new StringBuilder(255);
            GetPrivateProfileString(Section ?? EXE, Key, "", RetVal, 255, Path);
            return RetVal.ToString();
        }

        public void Write(string Key, string Value, string Section = null)
        {
            WritePrivateProfileString(Section ?? EXE, Key, Value, Path);
        }

        public void DeleteKey(string Key, string Section = null)
        {
            Write(Key, null, Section ?? EXE);
        }

        public void DeleteSection(string Section = null)
        {
            Write(null, null, Section ?? EXE);
        }

        public bool KeyExists(string Key, string Section = null)
        {
            return Read(Key, Section).Length > 0;
        }
    }

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
        private delegate void NoArgDelegate();

        public static void Refresh(DependencyObject obj)
        {

            obj.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,

            (NoArgDelegate)delegate { });

        }

        const int MAXCAMERA = 8;
        const int VIDEOW = 256;
        const int VIDEOH = 144;
        const string captureFolder = "./capture/";
        const string panoramaFolder = "./panorama/";
        const string settingFile = "./setting.ini";


        int cameraCounter = 0 ;

        panoramaONVIFVideo.onvif.Media2Client media;
		panoramaONVIFVideo.onvif.Media2Client[] medias;
        
		panoramaONVIFVideo.onvif.MediaProfile[] profiles;

        System.Windows.Forms.Timer panoramaTask;
 
		public MainWindow()
		{

			InitializeComponent();

            /// AddCamera();
            //address.Text = "195.60.68.239";
            //user.Text = "operator";
            //password.Password = "Onv!f2018";
			address.GotFocus += InitTextbox;
			user.GotFocus += InitTextbox;
			password.GotFocus += InitTextbox;
			button.Click += OnConnect;

            buttonAdd.Click += OnAdd;
            buttonLoad.Click += OnLoad;
            buttonSave.Click += OnSave;
            buttonAction.Click += OnAction;

            listBox.Items.Clear();
            listBox.SelectionChanged += OnSelectionChanged;

            medias = new panoramaONVIFVideo.onvif.Media2Client[24];

            System.IO.Directory.CreateDirectory(captureFolder);
            System.IO.Directory.CreateDirectory(panoramaFolder);

            DirectoryInfo directory = new DirectoryInfo(captureFolder);
            if (directory != null)
            {
                FileInfo[] files = directory.GetFiles();
                foreach (FileInfo file in files)
                {
                    File.Delete(file.FullName);
                }
            }
            panoramaTask = new System.Windows.Forms.Timer();
            panoramaTask.Interval = 2000; // specify interval time as you want
            panoramaTask.Tick += new EventHandler(timer_Tick);



		}

		private void OnConnect(object sender, RoutedEventArgs e)
		{

            buttonAdd.IsEnabled = false;
            //buttonLoad.IsEnabled = false;
            //buttonSave.IsEnabled = false;
            buttonAction.IsEnabled = false;
            listBox.Items.Clear();

        /////			var device = new device.DeviceClient(WsdlBinding, new EndpointAddress("http://" + address.Text + "/onvif_device"));onvif/device_service
			var device = new panoramaONVIFVideo.device.DeviceClient(WsdlBinding, new EndpointAddress("http://" + address.Text + "/onvif/device_service"));
            try
            {
                var services = device.GetServices(false);
			    var xmedia2 = services.FirstOrDefault(s => s.Namespace == "http://www.onvif.org/ver20/media/wsdl");
			    if (xmedia2 != null) {
                    listBox.Items.Clear();
                    media = new panoramaONVIFVideo.onvif.Media2Client(WsdlBinding, new EndpointAddress(xmedia2.XAddr));
                    media.ClientCredentials.HttpDigest.ClientCredential.UserName = user.Text;
                    media.ClientCredentials.HttpDigest.ClientCredential.Password = password.Password;
                    media.ClientCredentials.HttpDigest.AllowedImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Impersonation;
                    try
                    {
                        profiles = media.GetProfiles(null, null);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK, MessageBoxImage.Question);

                        /////Console.WriteLine("{0} Second exception caught.", ex.Message );
                    }
				    if (profiles != null) foreach (var p in profiles) listBox.Items.Add(p.Name);

                } 
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK, MessageBoxImage.Question);

                /////Console.WriteLine("{0} Second exception caught.", ex.Message );
            }

		}

		private void InitTextbox(object sender, RoutedEventArgs e)
		{
			if (((sender as Control).Foreground as SolidColorBrush).Color == Colors.DarkGray) {
				if (sender is TextBox) {
					(sender as TextBox).Text = "";
				}
				else if (sender is PasswordBox) {
					(sender as PasswordBox).Password = "";
				}
				(sender as Control).Foreground = new SolidColorBrush(Colors.Black);
			}
		}

		private void MediaPlayer_Log(object sender, Vlc.DotNet.Core.VlcMediaPlayerLogEventArgs e)
		{
			System.Diagnostics.Debug.WriteLine(string.Format("libVlc : {0} {1} @ {2}", e.Level, e.Message, e.Module));
		}

		private void OnVlcControlNeedsLibDirectory(object sender, Vlc.DotNet.Forms.VlcLibDirectoryNeededEventArgs e)
		{
			var currentAssembly = System.Reflection.Assembly.GetEntryAssembly();
			var currentDirectory = new FileInfo(currentAssembly.Location).DirectoryName;
			if (currentDirectory == null)
				return;
			if (IntPtr.Size == 4)
				e.VlcLibDirectory = new DirectoryInfo(System.IO.Path.Combine(currentDirectory, @"d:\Program Files (x86)\VideoLAN\VLC\")); 
			else
				e.VlcLibDirectory = new DirectoryInfo(System.IO.Path.Combine(currentDirectory, @"D:\temp\src\onvifex\Vlc.DotNet-develop\lib\x64")); 
		}


		private void OnSelectionChanged(object sender, RoutedEventArgs e)
		{
			if (profiles != null && listBox.SelectedIndex >= 0) 
            {
                buttonAdd.IsEnabled = true;
//                buttonLoad.IsEnabled = true;
//                buttonSave.IsEnabled = true;
                if (cameraCounter >= MAXCAMERA)
                    buttonAdd.IsEnabled = false;

			}
		}

		System.ServiceModel.Channels.Binding WsdlBinding
		{
			get
			{
				HttpTransportBindingElement httpTransport = new HttpTransportBindingElement();
				httpTransport.AuthenticationScheme = System.Net.AuthenticationSchemes.Digest;
				return new CustomBinding(new TextMessageEncodingBindingElement(MessageVersion.Soap12WSAddressing10, Encoding.UTF8), httpTransport);
			}
		}


        private void OnAdd(object sender, RoutedEventArgs e)
        {
            if (profiles != null && listBox.SelectedIndex >= 0 && cameraCounter < MAXCAMERA)
            {
                string[] xxx = address.Text.Split(':');
                string portNo = "80";
                if (xxx.Length > 1)
                {
                    portNo = xxx[1];
                };
                var uri = media.GetStreamUri("RtspOverHttp", profiles[listBox.SelectedIndex].token);
                ////                    textBox.Text = uri;
                uri = uri.Replace("http://", "rtsp://");

                string config  = address.Text;
                config = config + "|" + user.Text;
                config = config + "|" + password.Password;
                config = config + "|" + uri;

                AddCamera(config);
            };

        }

        private void OnDelete(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Do you want to removed this camera?", "Warning!!!", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                ///var xxx = (UIElement)GridMain.Children. .FindName("s0");
                ///GridMain.Children.Remove((UIElement)GridMain.FindName("s0"));
                for (int i = 0; i < GridMain.Children.Count; i++)
                {
                    if (GridMain.Children[i].GetType() == typeof(StackPanel))
                    {
 
                        // if (s.Name == "s" +count)
                        if (((StackPanel)GridMain.Children[i]).Children[2] == sender)
                        {
                            GridMain.Children.Remove(GridMain.Children[i]);
                            cameraCounter--;
                        }

                    }
                };
                reArrange();
            }
        }



        private void CombineImages(FileInfo[] files)
        {
            //change the location to store the final image.
            string finalImage = panoramaFolder + "FinalImage" +DateTime.Now.ToString("yyyyMMddhhmmss") +".png";
            int width = 0;
            int height = 0;
            foreach (FileInfo file in files)
            {
                System.Drawing.Image img = System.Drawing.Image.FromFile(file.FullName);

 
                width += img.Width;
   
                height = img.Height > height
                    ? img.Height
                    : height;

                img.Dispose();
            }



            Bitmap img3 = new Bitmap(width, height);
            Graphics g = Graphics.FromImage(img3);

            g.Clear(System.Drawing.SystemColors.AppWorkspace);

            width = 0;
            foreach (FileInfo file in files)
            {
                System.Drawing.Image img = System.Drawing.Image.FromFile(file.FullName);

                g.DrawImage(img, new System.Drawing.Point(width, 0));
                width += img.Width;

                img.Dispose();
            }

            g.Dispose();

            img3.Save(finalImage, System.Drawing.Imaging.ImageFormat.Png);
            img3.Dispose();


            System.Windows.Media.Imaging.BitmapImage newImage = new System.Windows.Media.Imaging.BitmapImage();
            newImage.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
            newImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.None;
            Uri urisource = new Uri(System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + finalImage, UriKind.RelativeOrAbsolute);
            newImage.BeginInit();
            newImage.UriSource = urisource;
            newImage.EndInit();
            combineImage.Width = width /3;
            combineImage.Height = height /3 ;

            combineImage.Source = newImage; 
            ///imageLocation.Image = System.Drawing.Image.FromFile(finalImage);
        }


        void timer_Tick(object sender, EventArgs e)
        {
            //Call method
            var c = 0;
            for (int i = 0; i < GridMain.Children.Count; i++)
            {
                if (GridMain.Children[i].GetType() == typeof(StackPanel))
                {

                    if (((StackPanel)GridMain.Children[i]).Children[0].GetType() == typeof(Vlc.DotNet.Wpf.VlcControl))
                    {
                        Vlc.DotNet.Wpf.VlcControl v = (Vlc.DotNet.Wpf.VlcControl)(((StackPanel)GridMain.Children[i]).Children[0]);
                        string filename = "snapshot" + c.ToString("000") + ".png";

                        TextBox t = (TextBox)(((StackPanel)GridMain.Children[i]).Children[1]);
                        if (v.MediaPlayer.GetCurrentMedia().State == Vlc.DotNet.Core.Interops.Signatures.MediaStates.Playing)
                        {
                            v.MediaPlayer.TakeSnapshot(captureFolder + filename);
                            t.Text = "Photo capture and save to " + captureFolder + filename + " !!!";
                        }
                        else
                        {
                            t.Text = "Camera Not Ready!!! State : " + v.MediaPlayer.GetCurrentMedia().State;
                        };
                        c++;
                    }

                }
            }

            //Change the path to location where your images are stored.
            DirectoryInfo directory = new DirectoryInfo(captureFolder);
            if (directory != null)
            {
                FileInfo[] files = directory.GetFiles();

                CombineImages(files);
            }

        }

        private void OnAction(object sender, RoutedEventArgs e)
        {
            if (panoramaTask.Enabled )
            {
                panoramaTask.Stop();
                buttonAction.Content = "Start Panorama";
                showCameras(true);
            }
            else
            {
                panoramaTask.Start();
                buttonAction.Content = "Stop Panorama";
                showCameras(false);
            }

        }


        private void OnLoad(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Do you want to load setting?", "Warning!!!", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {

                for (int i = GridMain.Children.Count - 1; i >= 0; i--)
                {
                    if (GridMain.Children[i].GetType() == typeof(StackPanel))
                    {
                        StackPanel s = (StackPanel)GridMain.Children[i];
                        GridMain.Children.Remove(s);
                    }
                }

                var MyIni = new IniFile(settingFile);

                cameraCounter = 0;
                var c = 0;

                string profile = "camera" + cameraCounter.ToString("000");
                while (MyIni.KeyExists("config", profile))
                {
                    var config = MyIni.Read("config", profile);

                    AddCamera(config);
                    c++;
                    profile = "camera" + cameraCounter.ToString("000");
                };
                reArrange();
            }
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Do you want to overwrite existing saved setting?", "Warning!!!", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {

                if (File.Exists(settingFile))
                {
                    File.Delete(settingFile);
                }

                var MyIni = new IniFile(settingFile);
                var c = 0;
                for (int i = 0; i < GridMain.Children.Count; i++)
                {
                    if (GridMain.Children[i].GetType() == typeof(StackPanel))
                    {
                        if (((StackPanel)GridMain.Children[i]).Children[0].GetType() == typeof(Vlc.DotNet.Wpf.VlcControl))
                        {
                            string profile = "camera" + c.ToString("000");
                            Label l = (Label)(((StackPanel)GridMain.Children[i]).Children[3]);
                            MyIni.Write("config", l.Content.ToString(), profile);
                            c++;
                        }
                    }
                }
            };

        }

        public void showCameras(bool showOn)
        {
            cameraCounter = 0;
            for (int i = 0; i < GridMain.Children.Count; i++)
            {
                if (GridMain.Children[i].GetType() == typeof(StackPanel))
                {
                    ((StackPanel)GridMain.Children[i]).Margin = new Thickness(134 + (cameraCounter % 4) * (VIDEOW + 10 * 2), 58 + (VIDEOH + 10 * 2 + 100) * (cameraCounter / 4), 0, 0); ///  StackPanel0.Margin;
                    ((StackPanel)GridMain.Children[i]).Name = "s" + cameraCounter;
                    if (showOn) 
                    {
                        ((StackPanel)GridMain.Children[i]).Visibility = Visibility.Visible;
                        combineImage.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        ((StackPanel)GridMain.Children[i]).Visibility = Visibility.Collapsed;
                        combineImage.Visibility = Visibility.Visible ;
                    }
                    cameraCounter++;
                }
            }
        }

        public void reArrange()
        {
            cameraCounter = 0;
            for (int i = 0; i < GridMain.Children.Count; i++)
            {
                if (GridMain.Children[i].GetType() == typeof(StackPanel))
                {
                    ((StackPanel)GridMain.Children[i]).Margin = new Thickness(134 + (cameraCounter % 4) * (VIDEOW + 10 * 2), 58 + (VIDEOH + 10 * 2 + 100) * (cameraCounter / 4), 0, 0); ///  StackPanel0.Margin;
                    ((StackPanel)GridMain.Children[i]).Name = "s" + cameraCounter;
                        
                    Vlc.DotNet.Wpf.VlcControl v = (Vlc.DotNet.Wpf.VlcControl)(((StackPanel)GridMain.Children[i]).Children[0]);
                    TextBox t = (TextBox)(((StackPanel)GridMain.Children[i]).Children[1]);
                    if (v.MediaPlayer.GetCurrentMedia().State == Vlc.DotNet.Core.Interops.Signatures.MediaStates.Playing)
                    {
                        t.Text = "Camera " + cameraCounter + " Ready!!!";
                    }
                    else
                    {
                        t.Text = "Camera Not Ready!!! State : " + v.MediaPlayer.GetCurrentMedia().State;
                    };

                    cameraCounter++;
                }
            }
        }


        public void AddCamera(string config)
        {
            string[] z = config.Split('|');
            try
            {
                var vlcCamera = new Vlc.DotNet.Wpf.VlcControl();
                vlcCamera.MediaPlayer.VlcLibDirectoryNeeded += OnVlcControlNeedsLibDirectory;
                vlcCamera.MediaPlayer.Log += MediaPlayer_Log;
                vlcCamera.MediaPlayer.EndInit();

                vlcCamera.MediaPlayer.Width = VIDEOW; ////  video.MediaPlayer.Width;
                vlcCamera.MediaPlayer.Height = VIDEOH; ///  video.MediaPlayer.Height;
                vlcCamera.HorizontalAlignment = HorizontalAlignment.Center;

                var txt = new TextBox();
                txt.HorizontalAlignment = HorizontalAlignment.Center;
                txt.Width = VIDEOW; ///  textBox.Width;
                txt.Height = 35; ///  textBox.Height;
                ////                txt.Background = Brushes.Transparent; // some attribute    
                txt.Margin = new Thickness(0, 5, 0, 0); ///  StackPanel0.Margin;
                txt.TextWrapping = TextWrapping.WrapWithOverflow;

                var but = new Button();
                but.HorizontalAlignment = HorizontalAlignment.Center;
                but.Width = VIDEOW; ///  textBox.Width;
                but.Height = 35; ///  textBox.Height;
                but.Content = "Remove this camera...";
                but.Margin = new Thickness(0, 5, 0, 0); ///  StackPanel0.Margin;

                but.Name = "b" + cameraCounter;
                but.Click += OnDelete;

                var lab = new Label();
                lab.HorizontalAlignment = HorizontalAlignment.Center;
                lab.Width = VIDEOW; ///  textBox.Width;
                lab.Height = 35; ///  textBox.Height;
                ////                txt.Background = Brushes.Transparent; // some attribute    
                lab.Margin = new Thickness(0, 5, 0, 0); ///  StackPanel0.Margin;
                lab.Visibility = Visibility.Collapsed;
                lab.Content = config;

                var stk = new StackPanel();
                stk.Orientation = Orientation.Vertical;
                stk.HorizontalAlignment = HorizontalAlignment.Left;
                stk.VerticalAlignment = VerticalAlignment.Top;
                stk.Width = VIDEOW + 10 * 2;
                stk.Height = VIDEOH + 10 * 2 + 100;
                stk.Children.Add(vlcCamera);
                stk.Children.Add(txt);
                stk.Children.Add(but);
                stk.Children.Add(lab);

                stk.Margin = new Thickness(134 + (cameraCounter % 6) * (VIDEOW + 10 * 2), 58 + (VIDEOH + 10 * 2 + 100) * (cameraCounter / 6), 0, 0); ///  StackPanel0.Margin;
                stk.Name = "s" + cameraCounter;

                GridMain.Children.Add(stk);

                try
                {
                    string[] xxx = z[0].Split(':');
                    string portNo = "80";
                    if (xxx.Length > 1)
                    {
                        portNo = xxx[1];
                    };
                    var uri = z[3];
                    ////                    textBox.Text = uri;
                    uri = uri.Replace("http://", "rtsp://");
                    string[] options = {
					                ":rtsp-http",
					                ":rtsp-http-port="+portNo ,
					                ":rtsp-user=" + z[1],
					                ":rtsp-pwd=" + z[2],
				                };


                    vlcCamera.MediaPlayer.Play(new Uri(uri), options);
                    int try3 = 6;
                    do
                    {
                        if (vlcCamera.MediaPlayer.GetCurrentMedia().State == Vlc.DotNet.Core.Interops.Signatures.MediaStates.Error)
                        {
                            if (--try3 == 0) break;
                            vlcCamera.MediaPlayer.Stop();
                            vlcCamera.MediaPlayer.Play(new Uri(uri), options);
                        }
                        Thread.Sleep(1000);
                    } while (vlcCamera.MediaPlayer.GetCurrentMedia().State != Vlc.DotNet.Core.Interops.Signatures.MediaStates.Playing);
                    if (vlcCamera.MediaPlayer.GetCurrentMedia().State == Vlc.DotNet.Core.Interops.Signatures.MediaStates.Playing)
                    {
                        txt.Text = "Camera " + cameraCounter + " Ready!!!";
                    }
                    else
                    {
                        txt.Text = "Camera " + cameraCounter + " Not Ready!!! State : " + vlcCamera.MediaPlayer.GetCurrentMedia().State;
                    };
                    Thread.Sleep(3000);
                    buttonAction.IsEnabled = true;

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK, MessageBoxImage.Question);

                    /////Console.WriteLine("{0} Second exception caught.", ex.Message );
                }

                Refresh(GridMain);
                cameraCounter++;

                buttonAdd.IsEnabled = false;
                listBox.Items.Clear();


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK, MessageBoxImage.Question);

                /////Console.WriteLine("{0} Second exception caught.", ex.Message );
            }

        }



    }
}
