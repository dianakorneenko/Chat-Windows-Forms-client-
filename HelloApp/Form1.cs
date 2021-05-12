using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Numerics;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using System.Threading;

namespace HelloApp
{
    public partial class Form1 : Form
    {
        static string userName;
        private const string host = "127.0.0.1";
        private const int port = 8888;
        static TcpClient client;
        static NetworkStream stream;
        bool alive;
        static byte[] key_fingerprint;
        static byte[] K;
        static Chat chat;

        public Form1()
        {
            InitializeComponent();

            loginButton.Enabled = true; // кнопка входа
            logoutButton.Enabled = false; // кнопка выхода
            sendButton.Enabled = false; // кнопка отправки
            chatTextBox.ReadOnly = true; // поле для сообщений
        }

        // обработчик нажатия кнопки loginButton
        private void loginButton_Click(object sender, EventArgs e)
        {
            userName = userNameTextBox.Text;
            userNameTextBox.ReadOnly = true;

            try
            {
                BigInteger random = RandomInteger(2048 / 8);

                client = new TcpClient();
                client.Connect(host, port); // подключение клиента
                stream = client.GetStream(); // получаем поток

                // получаем p и g от сервера
                BinaryReader reader = new BinaryReader(stream);

                string pString = reader.ReadString();
                BigInteger p = BigInteger.Parse(pString);

                BinaryWriter writer = new BinaryWriter(stream);
                writer.Write("gGO");

                string gString = reader.ReadString();
                BigInteger g = BigInteger.Parse(gString);

                writer.Write(userName);
                BigInteger A = BigInteger.ModPow(g, random, p);
                string go = reader.ReadString();

                // отправляем ключ А на сервер
                writer.Write(A.ToString());

                // получаем ключ B от второго клиента
                string BString = reader.ReadString();
                BigInteger B = BigInteger.Parse(BString);

                // вычисляем секретный общий ключ K
                BigInteger numberK = BigInteger.ModPow(B, random, p);
                K = numberK.ToByteArray();
                //MessageBox.Show(numberK.ToString());

                SHA256Managed hash = new SHA256Managed();
                byte[] key_hash = hash.ComputeHash(K);
                key_fingerprint = new byte[8];
                Buffer.BlockCopy(key_hash, key_hash.Length - 8, key_fingerprint, 0, key_fingerprint.Length);

                string usernameB = reader.ReadString();

                chat = new Chat(stream, client, K, usernameB, key_fingerprint, this.chatTextBox, this.messageTextBox);
                
                // запускаем задачу на прием сообщений
                Thread receiveThread = new Thread(new ThreadStart(chat.ReceiveMessage));
                receiveThread.Start(); //старт потока     

                loginButton.Enabled = false;
                logoutButton.Enabled = true;
                sendButton.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        // метод приема сообщений
        //private void ReceiveMessages()
        //{
        //    alive = true;
        //    try
        //    {
        //        while (alive)
        //        {
        //            IPEndPoint remoteIp = null;
        //            byte[] data = client.Receive(ref remoteIp);
        //            string message = Encoding.Unicode.GetString(data);

        //            // добавляем полученное сообщение в текстовое поле
        //            this.Invoke(new MethodInvoker(() =>
        //            {
        //                string time = DateTime.Now.ToShortTimeString();
        //                chatTextBox.Text = time + " " + message + "\r\n" + chatTextBox.Text;
        //            }));
        //        }
        //    }
        //    catch (ObjectDisposedException)
        //    {
        //        if (!alive)
        //            return;
        //        throw;
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(ex.Message);
        //    }
        //}

        // обработчик нажатия кнопки sendButton
        private void sendButton_Click(object sender, EventArgs e)
        {
            try
            {
                chat.SendMessage();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // обработчик нажатия кнопки logoutButton
        private void logoutButton_Click(object sender, EventArgs e)
        {
            ExitChat();
        }

        // выход из чата
        private void ExitChat()
        {
            string message = userName + " покидает чат";
            byte[] data = Encoding.Default.GetBytes(message);
            //client.Send(data, data.Length, HOST, REMOTEPORT);
            //client.DropMulticastGroup(groupAddress);

            alive = false;
            client.Close();

            loginButton.Enabled = true;
            logoutButton.Enabled = false;
            sendButton.Enabled = false;
        }
        // обработчик события закрытия формы
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (alive)
                ExitChat();
        }

        static BigInteger RandomInteger(int size)
        {
            using (var generator = RandomNumberGenerator.Create())
            {
                var salt = new byte[size];
                generator.GetBytes(salt);
                BigInteger number = FromBigEndian(salt);
                return number;
            }
        }
        public static BigInteger FromBigEndian(byte[] p)
        {
            return new BigInteger((p.Reverse().Concat(new byte[] { 0 })).ToArray());
        }

    }
}
