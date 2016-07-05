using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;

namespace 안드로이드_자동_데이터_백업
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private string folderPath;
        private const int port = 8282;
        iniUtil ini;

        public System.Windows.Forms.NotifyIcon notify;

        public MainWindow()
        {
            InitializeComponent();
        }

        public void setFolderPath(string path)
        {
            this.folderPath = path;
            saveDirBox.Text = this.folderPath;
        }

        public string getFolderPath()
        {
            return this.folderPath;
        }

        private void changeSaveDir_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                setFolderPath(dialog.SelectedPath);
                UpdateLogBox(dialog.SelectedPath + "로 파일저장 경로를 변경했습니다.");
            }
            saveSetting();
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            String curSaveLoc = AppDomain.CurrentDomain.BaseDirectory;
            setFolderPath(curSaveLoc);

            // 서버 시작.
            Thread th_server;
            th_server = new Thread(startServer);
            th_server.Start();

            // 내 외부 IP 갖고오기
            WebClient wc = new WebClient();
            wc.Encoding = Encoding.Default;
            string html = wc.DownloadString("http://ipip.kr");
            Regex regex = new Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}"); // 정규식
            Match m = regex.Match(html);
            myGlobalIP.Content = m.ToString();

            // 내 내부 IP 갖고오기
            string localIP;
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("10.0.2.4", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                localIP = endPoint.Address.ToString();
            }
            myLocalIP.Content = localIP;
        }

        public void UpdateLogBox(string data)
        {
            // 해당 쓰레드가 UI쓰레드인가?
            if (logBox.Dispatcher.CheckAccess())
            {
                //UI 쓰레드인 경우
                logBox.AppendText(data + Environment.NewLine);
                logBox.ScrollToLine(logBox.LineCount - 1); // 로그창 스크롤 아래로
            }
            else
            {
                // 작업쓰레드인 경우
                logBox.Dispatcher.BeginInvoke((Action)(() => { logBox.AppendText(data + Environment.NewLine); logBox.ScrollToLine(logBox.LineCount - 1); }));
            }
        }

        private void autoStart_Checked(object sender, RoutedEventArgs e)
        {
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(
                                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

            registryKey.SetValue("Phone2Computer", System.Reflection.Assembly.GetExecutingAssembly().Location);
            UpdateLogBox("시작 프로그램에 등록되었습니다.");
        }

        private void autoStart_Unchecked(object sender, RoutedEventArgs e)
        {
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(
                               @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            registryKey.DeleteValue("Phone2Computer", false);
            UpdateLogBox("시작 프로그램에서 해제되었습니다.");
        }

        private void saveSetting()
        {
            ini.SetIniValue("Setting", "AutoStart", autoStart.IsChecked.ToString());
            ini.SetIniValue("Setting", "downLocation", getFolderPath());
        }

        private void loadSetting()
        {
            string isAutoStart = ini.GetIniValue("Setting", "AutoStart");
            autoStart.IsChecked = Convert.ToBoolean(isAutoStart);

            setFolderPath(ini.GetIniValue("Setting", "downLocation")); // 다운 폴더 불러오기

            if (isAutoStart == "True")
            {
                ShowInTaskbar = false;
                this.WindowState = WindowState.Minimized;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 설정 저장파일 만들기
            string path = getFolderPath();  //프로그램 실행되고 있는데 path 가져오기
            string fileName = @"\config.ini";  //파일명
            string filePath = path + fileName;   //ini 파일 경로
            ini = new iniUtil(filePath);

            FileInfo fi = new FileInfo(filePath);
            if (fi.Exists)
            {
                loadSetting();
            }

            try
            {
                var menu = new System.Windows.Forms.ContextMenu();
                var item1 = new System.Windows.Forms.MenuItem();

                menu.MenuItems.Add(item1);

                item1.Index = 0;
                item1.Text = "프로그램 종료";
                item1.Click += delegate (object click, EventArgs eClick)
                {
                    this.Close();
                };

                notify = new System.Windows.Forms.NotifyIcon();
                notify.Icon = Properties.Resources.Icon1;
                notify.Visible = true;
                notify.DoubleClick += delegate (object senders, EventArgs args)
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                };
                notify.ContextMenu = menu;
                notify.Text = "Phone2Computer";
            }
            catch (Exception ee)
            {

            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            saveSetting();
        }

        private void eraseLogBtn_Click(object sender, RoutedEventArgs e)
        {
            logBox.Text = "로그를 삭제했습니다\n-----------------------------\n";
        }

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(@getFolderPath());
        }

        private void startServer()
        {
            IPEndPoint localAddress = new IPEndPoint(IPAddress.Any, port);
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            sock.Bind(localAddress);

            FileStream fileStream = null;
            string currentIP=null;

            UpdateLogBox("파일 업로드 서버 시작... ");

            while (true)
            {
                try
                {
                    byte[] buffer = new byte[4];

                    sock.Listen(1);
                    Socket clientSock = sock.Accept();

                    string connectedIP = (clientSock.RemoteEndPoint).ToString();

                    if (connectedIP.Equals(currentIP))
                    {                        
                        UpdateLogBox("클라이언트 접속 : " + connectedIP);
                        currentIP = connectedIP;
                    }

                    clientSock.Receive(buffer);
                    int fileNameLen = BitConverter.ToInt32(buffer, 0);

                    clientSock.Receive(buffer);
                    int fileSize = BitConverter.ToInt32(buffer, 0);

                    byte[] clientData = new byte[fileSize];

                    buffer = new byte[fileNameLen]; // 버퍼 크기 새로 지정
                    clientSock.Receive(buffer);
                    string fileName = Encoding.UTF8.GetString(buffer, 0, fileNameLen);
                    
                    buffer = new byte[1500]; // 버퍼 크기 새로 지정
                    
                    String curSaveLoc = getFolderPath();
                    curSaveLoc = curSaveLoc + @"\Phone2Computer";
                    DirectoryInfo di = new DirectoryInfo(curSaveLoc);

                    if (di.Exists == false)
                    {
                        di.Create(); // Phone2Computer 폴더 생성
                    }
                    
                    string filePath = curSaveLoc + "/" + fileName;
                    
                    FileInfo fi = new FileInfo(filePath);
                    if(fi.Exists) // 파일 존재
                    {
                        UpdateLogBox(fileName+" 해당 파일이 이미 존재하기에 전송이 취소되었습니다.");
                    }
                    else // 파일이 없으면
                    {
                        int curSize = 0;// 현재까지 받은 파일 크기

                        fileStream = new FileStream(filePath, FileMode.Create);

                        //파일 수신
                        while (curSize < fileSize)
                        {
                            int receiveLength = clientSock.Receive(buffer);
                            fileStream.Write(buffer, 0, receiveLength);
                            curSize += receiveLength;
                        }

                        fileStream.Close();

                        if(curSize!=fileSize)
                        {
                            UpdateLogBox("\n파일 전송에 문제가 생겼습니다.\n네트워크 환경을 체크해주세요");
                        }
                        else
                        {
                            UpdateLogBox(fileName + "이 전송되었습니다.");
                            UpdateLogBox("수신파일 크기 : " + fileSize+"bytes");
                        }                        
                    }
                    
                    clientSock.Close();                    
                }
                catch (Exception ex)
                {
                    UpdateLogBox("오류 발생 : " + ex);
                }
            }
        }
    }
}
