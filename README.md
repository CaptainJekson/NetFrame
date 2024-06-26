# NetFrame

![photo_2023-12-01 11 37 15](https://github.com/CaptainJekson/NetFrame/assets/55331074/36860eca-4c0d-47ba-aaca-6c805103ff54)


Simple networking framework for Unity games

## 📖 How To Install

### Unity Engine

Using package manager by URL:
https://github.com/CaptainJekson/NetFrame.git?path=/Packages/com.jeskvo.net-frame

### .Net Platform

Using nuget package:
https://www.nuget.org/packages/NetFrame

## 📖 Starting

### Server

```c#
_server = new NetFrameServer(2000);
_server.Start(8080, 10);
```
To start the server, create an instance of the server, 
passing the maximum packet size, and call the `Start()` 
method, passing the port number and maximum number of connected clients.


```c#
private void Update()
{
    _server.Run(100);
}
```

Also, it is necessary to call the `Run()` method every frame 
for Unity or in an infinite loop for .NET to maintain the session 
and check for available packets to read. You can set a cooldown to control how often this 
method is called in order to avoid calling it too frequently. 
Use the limit parameter to avoid deadlocks.

```c#
_server.ClientConnection += OnClientConnection;
_server.ClientDisconnect += OnClientDisconnect;
```
The `ClientConnection` and `ClientDisconnect` events 
are triggered when a client connects and disconnects, respectively.

```c#
_server.LogCall += OnLog;
```

The `LogCall` event is triggered to display server logs, warnings, and errors.

```c#
_server.Stop();
```

Call `Stop` to stop the server. 
For example, in the OnApplicationQuit() method in Unity.

### Client

```c#
_client = new NetFrameClient();
_client.Connect("127.0.0.1", 8080);
```

To connect a client to the server, create an instance of the 
server and call the `Connect()` method, passing the 
IP address and port number as parameters.

```c#
private void Update()
{
    _server.Run(100);
}
```

Similarly to the server, you need to call the Run() method 
every frame for Unity or in an infinite loop for .NET to maintain the session.

```c#
_client.ConnectionSuccessful += OnConnectionSuccessful;
_client.Disconnected += OnDisconnected;
_client.LogCall += OnLog;
_client.ConnectionFailed += OnConnectionFailed;
```

The `ConnectionSuccessful` event is triggered when a connection to the server 
is successful. `Disconnected` is triggered when the client disconnects, 
for example, if the `Disconnect()` method is called manually.
`LogCall` is used to display logs, similar to the server.
`ConnectionFailed` called when there is an error connecting to the server


## 📖 Dataframes

### ⚠️ Initialize ⚠️

It is necessary to initialize the packets at the start of the application 
to create a collection of them. Specify the current project assembly 
in which the dataframes will be created.

```c#
NetFrameDataframeCollection.Initialize(Assembly.GetExecutingAssembly());
```
### Sending

To create a packet (called dataframes in NetFrame), you need to create a 
structure that implements the `INetworkDataframe` interface. For example:

```c#
public struct TestDataframe : INetworkDataframe
{
    public string Name;
    public byte Age;

    public void Write(NetFrameWriter writer)
    {
        writer.WriteString(Name);
        writer.WriteByte(Age);
    }


    public void Read(NetFrameReader reader)
    {
        Name = reader.ReadString();
        Age = reader.ReadByte();
    }
}
```

Dataframes for reading and writing support all standard C# types. 
To send a data frame from the client to the server, create an instance 
of the data frame and call the `_client.Send()` method.

```c#
var testDataframe = new TestDataframe
{
    Name = "Kate",
    Age = 25,
};
_client.Send(ref testDataframe);
```

On the server, you should call the `_server.Send()` method to send 
to a specific client with a specific identifier or send to all 
clients simultaneously using `_server.SendAll()`. You can also use
`SendAllExcept()` to send to all clients except one.

```c#
var testDataframe = new TestDataframe
{
    Name = "Kate",
    Age = 25,
};
_server.Send(ref testDataframe, 1); //send to client with id = 1
_server.SendAll(ref testDataframe); //send to all clients
_server.SendAllExcept(ref testDataframe, 1); //send to all clients except client with Id = 1
```

### Listen

To subscribe or unsubscribe to the event of receiving a packet on the client 
and server, you need to use the `Subscribe` and `Unsubscribe` methods, 
respectively.

```c#
_client.Subscribe<TestDataframe>(TestDataframeHandler);

_client.Unsubscribe<TestDataframe>(TestDataframeHandler);
```

You also need to define a handler method for this event. For example:

```c#
private void TestDataframeHandler(TestDataframe dataframe)
{
    Debug.Log($"Name: {dataframe.Name} Age: {dataframe.Age}");
}
```

On the server, it is the same, except that the client's Id will be passed 
to the handler method:

```c#
private void TestDataframeHandler(TestDataframe dataframe, int id)
{
    Debug.Log($"Client id: {id} Name: {dataframe.Name} Age:     {dataframe.Age}");
}
```

## 📖 Sending a collection in a dataframe

First, create a structure that will be an element of the collection. 
Implement the `IWriteable` and `IReadable` interfaces.

```c#
public struct UserNetworkModel : IWriteable, IReadable
{
    public string Name;
    public uint Age;

    public void Write(NetFrameWriter writer)
    {
        writer.WriteString(Name);
        writer.WriteUInt(Age);
    }


    public void Read(NetFrameReader reader)
    {
        Name = reader.ReadString();
        Age = reader.ReadUInt();
    }
}
```

Next, define a dataframe with the collection, for example, a `List` type, 
as follows:

```c#
public struct UsersDataframe : INetworkDataframe
{
    public List<UserNetworkModel> Users;

    public void Write(NetFrameWriter writer)
    {
       var hasUsers = Users != null;
       writer.WriteBool(hasUsers);

       if (hasUsers)
       {
           writer.WriteInt(Users.Count);

           foreach (var user in Users)
           {
               writer.Write(user);
           }
       }
    }


    public void Read(NetFrameReader reader)
    {
        if (reader.ReadBool())
        {
            var count = reader.ReadInt();
            Users = new List<UserNetworkModel>();

            for (var i = 0; i < count; i++)
            {
                Users.Add(reader.Read<UserNetworkModel>());
            }
        }
    }
}
```

Using the same analogy, you can implement data frames with collections of
`Dictionary` and other types.

## 📖 Protected connection

Secure client connections to the server can be established using RSA encryption. 
This effectively prevents unauthorized access to your server.

First, you need to generate a pair of RSA keys. Use a key generator:

Key generator for Windows: [NetFrameKeyGeneratorForWindows.zip](https://github.com/user-attachments/files/16010110/NetFrameKeyGeneratorForWindows.zip)

Key generator for MacOS: [NetFrameKeygenForMacOS.zip](https://github.com/user-attachments/files/16010106/NetFrameKeygenForMacOS.zip)

Two files will be generated: `publicRSAKey.xml` и `privateRSAKey.xml`.
The public key must be placed on the client, and the private key on the server.

On the server, before the Start method is called, call:
```c#
_netFrameServer.Start(8080, 10);
_netFrameServer.SetProtectionWithFilePath(Application.dataPath + "/RSAKeys/privateRSAKey.xml", "fk2kgb3kggl3jgl3nlg3g312")
```
Or if you need to pass the XML as a string:
```c#
_netFrameServer.Start(8080, 10);
_netFrameServer.SetProtectionWithXml(rsaXmlParameters, "fk2kgb3kggl3jgl3nlg3g312")
```

On the client, before the Connect method is called, call:
```c#
_netFrameClient.Connect("127.0.0.1", 8080);
_netFrameClient.SetProtectionWithFilePath(Application.dataPath + "/RSAKeys/privateRSAKey.xml", "fk2kgb3kggl3jgl3nlg3g312")
```
Or if you need to pass the XML as a string:
```c#
_netFrameClient.Connect("127.0.0.1", 8080);
_netFrameClient.SetProtectionWithXml(rsaXmlParameters, "fk2kgb3kggl3jgl3nlg3g312")
```

The `SetProtectionWithFilePath` method specifies the path to the key and token.
The `SetProtectionWithXml` method specifies the content of the XML file as a string and the token.
The token will be encrypted on the client and decrypted on the server using RSA keys.
If these methods are not called, the secure connection will not work.
