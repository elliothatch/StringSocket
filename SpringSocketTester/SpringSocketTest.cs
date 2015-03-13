//SpringSocketTest.cs - Elliot Hatch and Samuel Davidson - November 2014
using CustomNetworking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

namespace StringSocketTester
{


    /// <summary>
    ///This is a test class for StringSocketTest and is intended
    ///to contain all StringSocketTest Unit Tests
    ///</summary>
    [TestClass()]
    public class StringSocketTest
    {
        static int testPort;
        static object testPortLock;
        static int getPort() { lock (testPortLock) { return testPort++; } }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        // 
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        [ClassInitialize()]
        public static void MyClassInitialize(TestContext testContext)
        {
            testPortLock = new Object();
            testPort = 4001;
        }

        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //Use TestInitialize to run code before running each test
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{
        //}
        //
        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //
        #endregion

        /// <summary>
        ///A simple test for BeginSend and BeginReceive
        ///</summary>
        [TestMethod()]
        public void Test1()
        {
            new Test1Class().run(getPort());
        }

        /// <summary>
        /// Tests that the receive callback doesn't block
        /// </summary>
        [TestMethod()]
        public void Test2()
        {
            int timeout = 2000;
            StringSocket sendSocket;
            StringSocket receiveSocket;
            string s1 = "";
            int p1 = 0;
            string s2 = "";
            int p2 = 0;

            ManualResetEvent mre1 = new ManualResetEvent(false);
            ManualResetEvent mre2 = new ManualResetEvent(false);

            setupServerClient(getPort(), out sendSocket, out receiveSocket);

            receiveSocket.BeginReceive((s, e, p) => { s1 = s; p1 = (int)p; mre1.Set(); Thread.Sleep(10000); }, 1);
            receiveSocket.BeginReceive((s, e, p) => { s2 = s; p2 = (int)p; mre2.Set(); }, 2);
            sendSocket.BeginSend("Hello\nWorld\n", (e, p) => { }, null);

            Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
            Assert.AreEqual("Hello", s1);
            Assert.AreEqual(1, p1);

            //if the StringSocket doesn't run the callback on its own thread, this assert will fail
            Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
            Assert.AreEqual("World", s2);
            Assert.AreEqual(2, p2);

        }

        /// <summary>
        /// Tests receive-then-send and send-then-recieve message/payload order
        /// </summary>
        [TestMethod()]
        public void Test3()
        {
            StringSocket sendSocket;
            StringSocket receiveSocket;

            ManualResetEvent mre1 = new ManualResetEvent(false);
            ManualResetEvent mre2 = new ManualResetEvent(false);

            setupServerClient(getPort(), out sendSocket, out receiveSocket);

            for (int i = 0; i < 1000; i++)
                receiveSocket.BeginReceive((s, e, p) => { Assert.AreEqual((int)p, Int32.Parse(s)); }, i);

            for (int i = 0; i < 1000; i++)
                sendSocket.BeginSend(i + "\n", (e, p) => { }, null);

            for (int i = 0; i < 1000; i++)
                sendSocket.BeginSend(i + "\n", (e, p) => { }, null);

            for (int i = 0; i < 1000; i++)
                receiveSocket.BeginReceive((s, e, p) => { Assert.AreEqual((int)p, Int32.Parse(s)); }, i);
        }


        /// <summary>
        /// Tests that you can handle sending and receive 1000 messages.
        /// </summary>
        [TestMethod()]
        public void Test4()
        {
            new TestGnarlyClass().run(getPort());
        }
        public class TestGnarlyClass
        {
            StringSocket sendSocket;
            StringSocket receiveSocket;

            // Messages Recieved
            int messagesReceived = 0;
            int messagesSent = 0;

            public void run(int port)
            {
                ManualResetEvent mre1 = new ManualResetEvent(false);
                ManualResetEvent mre2 = new ManualResetEvent(false);

                // Create and start a server and client.
                TcpListener server = null;
                TcpClient client = null;
                server = new TcpListener(IPAddress.Any, port);
                server.Start();
                client = new TcpClient("localhost", port);

                // Obtain the sockets from the two ends of the connection. We are using the blocking AcceptSocket()
                // method here, which is OK for a test case.
                Socket serverSocket = server.AcceptSocket();
                Socket clientSocket = client.Client;

                // Wrap the two ends of the connection into StringSockets
                sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());


                for (int x = 0; x < 1000; x++)
                {
                    receiveSocket.BeginReceive((s, e, p) =>
                    {
                        lock (this)
                        {
                            messagesReceived = messagesReceived + 1;
                            if (messagesReceived == 1000)
                            {
                                Assert.AreEqual(1000, messagesSent, "Assert that the 1000 messages sent were sent");
                                Assert.AreEqual(1000, messagesReceived, "Assert that the 1000 messages sent were recieved");
                            }
                        }
                    }, x);
                }
                for (int x = 0; x < 1000; x++)
                {
                    sendSocket.BeginSend("Message: " + x + "\n", (e, p) =>
                    {
                        lock (this)
                        {
                            messagesSent = messagesSent + 1;
                        }
                    }, x);
                }

            }
        }

        /// <summary>
        /// Function that sets up the server and client for a TCP test
        /// </summary>
        /// <param name="port"></param>
        /// <param name="sendSocket"></param>
        /// <param name="receiveSocket"></param>
        public void setupServerClient(int port, out StringSocket sendSocket, out StringSocket receiveSocket)
        {
            // Create and start a server and client.
            TcpListener server = null;
            TcpClient client = null;
            server = new TcpListener(IPAddress.Any, port);
            server.Start();
            client = new TcpClient("localhost", port);

            // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
            // method here, which is OK for a test case.
            Socket serverSocket = server.AcceptSocket();
            Socket clientSocket = client.Client;

            // Wrap the two ends of the connection into StringSockets
            sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
            receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

        }


        public class Test1Class
        {
            // Data that is shared across threads
            private ManualResetEvent mre1;
            private ManualResetEvent mre2;
            private String s1;
            private object p1;
            private String s2;
            private object p2;

            // Timeout used in test case
            private static int timeout = 2000;

            public void run(int port)
            {
                // Create and start a server and client.
                TcpListener server = null;
                TcpClient client = null;

                try
                {
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);
                    mre2 = new ManualResetEvent(false);

                    // Make two receive requests
                    receiveSocket.BeginReceive(CompletedReceive1, 1);
                    receiveSocket.BeginReceive(CompletedReceive2, 2);

                    // Now send the data.  Hope those receive requests didn't block!
                    String msg = "Hello world\nThis is a test\n";
                    foreach (char c in msg)
                    {
                        sendSocket.BeginSend(c.ToString(), (e, o) => { }, null);
                    }

                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("Hello world", s1);
                    Assert.AreEqual(1, p1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("This is a test", s2);
                    Assert.AreEqual(2, p2);
                }
                finally
                {
                    server.Stop();
                    client.Close();
                }
            }

            // This is the callback for the first receive request.  We can't make assertions anywhere
            // but the main thread, so we write the values to member variables so they can be tested
            // on the main thread.
            private void CompletedReceive1(String s, Exception o, object payload)
            {
                s1 = s;
                p1 = payload;
                mre1.Set();
            }

            // This is the callback for the second receive request.
            private void CompletedReceive2(String s, Exception o, object payload)
            {
                s2 = s;
                p2 = payload;
                mre2.Set();
            }
        }
    }
}
