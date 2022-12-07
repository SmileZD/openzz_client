using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using TouchSocket;
using TouchSocket.Core.Config;
using TouchSocket.Core.Dependency;
using TouchSocket.Core.Log;
using TouchSocket.Http.WebSockets;
using TouchSocket.Sockets;

namespace 开源中转
{
    public partial class Form1 : Form
    {

        String pool_host;//矿池地址
        String pool_port;//矿池端口
        String server_host;//服务器地址
        String server_port;//服务器端口
        String server_ssl;//服务器端口是否是ssl
        String secretKey;//服务器密钥
        bool zq;//设置自启
        bool islianjie;//是否已经连接
        byte[] key;
        String port;//本地端口

        WebSocketClient myWSClient;//ws连接客户端对象
        TcpService service;//tcp服务器对象

        public Form1()
        {
            InitializeComponent();
        }
        private void Getvalue()
        {
            pool_host = textBox4.Text;
            pool_port = textBox3.Text;
            server_host = textBox1.Text;
            server_port = textBox2.Text;
            secretKey = textBox5.Text;
            port = textBox6.Text;
            key = Encoding.UTF8.GetBytes(secretKey);
            if (radioButton1.Checked){
                server_ssl = "ssl";
            }else{
                server_ssl = "tcp";
            }
            Microsoft.Win32.RegistryKey HKLM = Microsoft.Win32.Registry.CurrentUser;
            Microsoft.Win32.RegistryKey Bconfig = HKLM.CreateSubKey(@"Software\Openzz\config");
            try
            {
                if (Bconfig != null)
                {
                    Bconfig.SetValue("PoolHost", textBox4.Text);
                    Bconfig.SetValue("PoolPort", textBox3.Text);
                    Bconfig.SetValue("ServerHost", textBox1.Text);
                    Bconfig.SetValue("ServerPort", textBox2.Text);
                    Bconfig.SetValue("SecretKey", textBox5.Text);
                    Bconfig.SetValue("Port", textBox6.Text);
                    Bconfig.SetValue("PoolHost", textBox4.Text);
                    if (radioButton2.Checked){Bconfig.SetValue("SSL", "1");}else{Bconfig.SetValue("SSL", "2");}
                    Bconfig.Close();
                }
            }
            catch (Exception) { }
        }
        private void Button1_Click(object sender, EventArgs e)
        {
            Button1_Click2();
        }
        public void Button1_Click2()
        {
            try
            {
                if (button1.Text == "未连接/启动")
                {
                    Getvalue();
                    Close_All();
                    Start_Ws_Client();
                    Start_Server();
                    button1.Text = "已连接/停止";
                    islianjie=true;
                    List<string> ipv4_ips = GetLocalIpAddress("InterNetwork");
                    label8.Text = "";
                    foreach (string ip in ipv4_ips)
                    {
                        if (!ip.EndsWith(".1"))
                        {
                            label8.Text += "stratum+tcp://" + ip  + ":" + port +"\n";
                        }
                    }
                }
                else
                {
                    islianjie = false;
                    Close_All();
                    button1.Text = "未连接/启动";
                    label8.Text = "";
                    
                }
            }
            catch (Exception eee)
            {
                Console.WriteLine(eee);
                if (islianjie)
                {
                    tryagain();
                }
                else
                {
                    MessageBox.Show("连接失败");
                    Close_All();
                    button1.Text = "未连接/启动";
                    label8.Text = "";
                    islianjie = false;
                }
                
            }

        }
        private void Close_All()
        {
            try { myWSClient.Close(); } catch (Exception) { }
            try { service.Stop(); } catch (Exception) { }
        }
        private void Start_Ws_Client()
        {
            myWSClient = new WebSocketClient();
            TouchSocketConfig myWSConfig = new TouchSocketConfig();
            try {
                myWSConfig.SetRemoteIPHost(new IPHost(server_host + ":" + server_port));
            }
            catch(Exception){
                myWSConfig.SetRemoteIPHost(new IPHost(Dns.GetHostAddresses(server_host)[0].ToString() + ":" + server_port));
            }
            myWSClient.Setup(myWSConfig);
            try { myWSClient.Connect(); } catch (Exception) {
                throw new Exception();
            }
            myWSClient.Received += (c, ee) =>
            {
                switch (ee.Opcode)
                {
                    case WSDataType.Cont:
                        break;
                    case WSDataType.Text:
                        HandleWSInfo(ee.PayloadData.ToString());
                        break;
                    case WSDataType.Binary:
                        break;
                    case WSDataType.Close:
                        break;
                    case WSDataType.Ping:
                        break;
                    case WSDataType.Pong:
                        break;
                    default:
                        break;
                }

            };
            myWSClient.Disconnected += (c, ee) =>
            {
                if (islianjie)
                {
                    tryagain();
                }
               
            };
        }
        private void tryagain()
        {
            Console.WriteLine("1\n");
            try
            {
                Start_Ws_Client();
                try { service.Stop(); } catch (Exception) { }
                Start_Server();
                button1.Text = "已连接/停止";
                islianjie = true;
                List<string> ipv4_ips = GetLocalIpAddress("InterNetwork");
                label8.Text = "";
                foreach (string ip in ipv4_ips)
                {
                    if (!ip.EndsWith(".1"))
                    {
                        label8.Text += "stratum+" + server_ssl + "://" + ip + ":" + port + "\n";
                    }
                }
            }
            catch (Exception eee)
            {
                Console.WriteLine(eee);
                if (islianjie)
                {
                    tryagain();
                }
                else
                {
                    MessageBox.Show("连接失败");
                    Close_All();
                    button1.Text = "未连接/启动";
                    label8.Text = "";
                    islianjie = false;
                }

            }
        }
        private void Start_Server()
        {
            service = new TcpService();
            service.Connecting += (client, e) => { };//有客户端正在连接
            service.Connected += (client, e) => { HandleTcpFirst(client.ID); };//有客户端连接
            service.Disconnected += (client, e) => { };//有客户端断开连接
            service.Received += (client, byteBlock, requestInfo) => { HandleTcpInfo(client.ID, byteBlock.ToString()); };
            try
            {
                TouchSocketConfig myService = new TouchSocketConfig();
                myService.SetListenIPHosts(new IPHost[] { new IPHost(int.Parse(port)) });
                myService.SetMaxCount(10000);
                myService.SetThreadCount(100);
                service.Setup(myService).Start();
            }
            catch (Exception e)
            {
                MessageBox.Show("端口"+ port+"被占用");
            }

        }
        private void HandleWSInfo(string mes)
        {
            try {
                if (mes.Length > 0)
                {
                    char temp1 = mes[0];
                    mes = mes.Substring(1, mes.Length - 1);//去掉首位
                    string temp2 = Em(mes);
                    if (temp1 != 'e')
                    {
                        string[] temp3 = temp2.Split('$');
                        service.Send(temp3[0], Encoding.UTF8.GetBytes(temp3[1]));
                    }
                }
            } catch (Exception) { }
        }
        private void HandleTcpInfo(string ClientID, string mes)
        {
            byte[] temp1 = Am(ClientID + "$" + mes);
            byte[] temp2 = Encoding.UTF8.GetBytes("o");
            byte[] temp3 = new byte[temp1.Length + temp2.Length];
            temp2.CopyTo(temp3, 0);
            temp1.CopyTo(temp3, temp2.Length);
            myWSClient.SendWithWS(temp3);
        }
        private void HandleTcpFirst(string ClientID)
        {
            byte[] temp1;
            if (server_ssl == "tcp")
            {
                temp1 = Am(ClientID + "$" + pool_host + "$" + pool_port + "$1");
            }
            else
            {
                temp1 = Am(ClientID + "$" + pool_host + "$" + pool_port + "$2");
            }
            byte[] temp2 = Encoding.UTF8.GetBytes("s");
            byte[] temp3 = new byte[temp1.Length + temp2.Length];
            temp2.CopyTo(temp3, 0);
            temp1.CopyTo(temp3, temp2.Length);
            myWSClient.SendWithWS(temp3);
        }
        //加密
        public byte[] Am(string content)
        {
            byte[] data = Encoding.UTF8.GetBytes(content);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= key[i % key.Length];
            }
            return data;
        }
        //解密
        public string Em(string content)
        {
            byte[] data = Encoding.UTF8.GetBytes(content);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= key[i % key.Length];
            }
            return Encoding.UTF8.GetString(data, 0, data.Length);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;
            zq = false;
            islianjie=false;
            Microsoft.Win32.RegistryKey HKLM = Microsoft.Win32.Registry.CurrentUser;
            Microsoft.Win32.RegistryKey Run = HKLM.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            Microsoft.Win32.RegistryKey Bconfig = HKLM.OpenSubKey(@"Software\Openzz\config");
            try {
                if (Bconfig != null)
                {
                    textBox4.Text = (string)Bconfig.GetValue("PoolHost");
                    textBox3.Text = (string)Bconfig.GetValue("PoolPort");
                    textBox1.Text = (string)Bconfig.GetValue("ServerHost");
                    textBox2.Text = (string)Bconfig.GetValue("ServerPort");
                    textBox5.Text = (string)Bconfig.GetValue("SecretKey");
                    textBox6.Text = (string)Bconfig.GetValue("Port");
                    if ((string)Bconfig.GetValue("SSL") == "1"){radioButton2.Checked = true;}else { radioButton1.Checked = true; }
                    Bconfig.Close();
                }
                string temp = Run.GetValue("openzz").ToString();
                if (temp == Application.ExecutablePath)
                {
                    zq = true;
                    Button1_Click2();
                }
            }
            catch(Exception) {}
        }

        public static List<string> GetLocalIpAddress(string netType)
        {
            string hostName = Dns.GetHostName();
            IPAddress[] addresses = Dns.GetHostAddresses(hostName); 

            List<string> IPList = new List<string>();
            if (netType == string.Empty)
            {
                for (int i = 0; i < addresses.Length; i++)
                {
                    IPList.Add(addresses[i].ToString());
                }
            }
            else
            {
                for (int i = 0; i < addresses.Length; i++)
                {
                    if (addresses[i].AddressFamily.ToString() == netType)
                    {
                        IPList.Add(addresses[i].ToString());
                    }
                }
            }
            return IPList;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(linkLabel1.Text);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (zq)
            {
                RunWhenStart(false, "openzz", Application.ExecutablePath);
                MessageBox.Show("已取消开机自启");
                zq= false;
            }
            else
            {
                RunWhenStart(true, "openzz", Application.ExecutablePath);
                MessageBox.Show("已设置开机自启");
                zq = true;
            }
            
        }
        public void RunWhenStart(bool Started, string name, string path)
        {
            Microsoft.Win32.RegistryKey HKLM = Microsoft.Win32.Registry.CurrentUser;
            Microsoft.Win32.RegistryKey Run = HKLM.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            if (Started == true)
            {
                try
                {
                    Run.SetValue(name, path);
                    HKLM.Close();
                }
                catch { }
            }
            else
            {
                try
                {
                    Run.DeleteValue(name);
                    HKLM.Close();
                }
                catch { }
            }
        }
    }
}
