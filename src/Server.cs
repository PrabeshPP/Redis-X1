using System.Net;
using System.Net.Sockets;
using System.Text;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

// Uncomment this block to pass the first stage
TcpListener server = new TcpListener(IPAddress.Any, 6379);
server.Start();
Socket socket = server.AcceptSocket(); // wait for client


string responseTxt = "+PONG\r\n";
byte[] responseData = Encoding.UTF8.GetBytes(responseTxt);

while(socket.Connected){
    byte[] buffer = new byte[1024];
    await socket.ReceiveAsync(buffer,SocketFlags.None);
    await socket.SendAsync(responseData,SocketFlags.None);
}

