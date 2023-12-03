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
_server = new NetFrameServer();
_server.Start(8080, 10);
```
Для запуска сервера создайте экземпляр сервера и вызовите метод Start()
передав в качестве параметров номер порта и количество максимально
подключенных клиентов.

```c#
private void Update()
{
    _server.Run();
}
```

Также необходимо вызывать метод `Run()` каждый кадр для Unity или в
бесконечном цикле для .NET для поддержания сессии и для проверки
доступных пакетов для чтения. Самостоятельно можно установить кулдаун
для вызова этого метода, чтобы не вызывать слишком часто.

```c#
_server.ClientConnection += OnClientConnection;
_server.ClientDisconnect += OnClientDisconnect;
```
События ClientConnection и ClientDisconnect вызываются при подключении
и отключении клиента соответственно.

```c#
_server.Stop();
```
Вызовите Stop для остановки работы сервера. Например в OnApplicationQuit()
метода в Unity.

### Client

```c#
_client = new NetFrameClient();
_client.Connect("127.0.0.1", 8080);
```
Для подключения клиента к серверу создайте экземпляр сервера и
вызовите метод Connect() передав в качестве параметров ip адрес и
номер порта.

```c#
private void Update()
{
    _server.Run();
}
```
По аналогии с сервером нужно вызывать метод Run() каждый
кадр для Unity или в бесконечном цикле для .NET для поддержания сессии.

```c#
_client.ConnectionSuccessful += OnConnectionSuccessful;
_client.ConnectedFailed += OnConnectedFailed;
_client.Disconnected += OnDisconnected;
```

Событие ConnectionSuccessful вызывается при успешном подключении к серверу. ConnectedFailed вызывается при ошибке соединения с сервером:
AlreadyConnected - соединение с сервером уже установлено
ImpossibleToConnect - не удаёться установить соединение с сервером.
ConnectionLost - соединение было прервано.
Disconnected вызывается при отключении от сервера клиентом. Например вручную вызвали метод Disconnect().

## 📖 Dataframes

### Initialize

**Обязательно** нужно вызвать инициализацию пакетов при самом старте
приложения. Чтобы была создана коллекция из них. При этом указав текущую сборку проекта в котором будут
создаваться датафреймы.

```c#
NetFrameDataframeCollection.Initialize(Assembly.GetExecutingAssembly());
```
### Sending

Для того чтобы создать пакет (в NetFrame они называються датафреймами). Нужно создать структуру которая реализует интерфейс INetworkDataframe.
Например:

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

Датафреймы для записи и чтения поддерживают все стандартные C# типы. Чтобы отправить датафрейм с клиента на сервер, нужно
создать экземпляр датафрейма и вызвать метод _client.Send().

```c#
var testDataframe = new TestDataframe
{
    Name = "Kate",
    Age = 25,
};
_client.Send(ref testDataframe);
```

На сервер аналогично нужно вызвать метод _server.Send()
для отправки клиенту с конкретным id либо отправить сразу всем
клиентам с помощью _server.SendAll().

```c#
var testDataframe = new TestDataframe
{
    Name = "Kate",
    Age = 25,
};
_server.Send(ref testDataframe, 1); //send to client with id = 1
_server.SendAll(ref testDataframe); //send to all clients
```

### Listen

На клиенте и сервере чтобы подписаться или отписаться на событие получения пакета нужно
использовать методы Subscribe и Unsubscribe соответственно.

```c#
_client.Subscribe<TestDataframe>(TestDataframeHandler);

_client.Unsubscribe<TestDataframe>(TestDataframeHandler);
```

Также нужно определить метод обработчик этого события. Например:
```c#
private void TestDataframeHandler(TestDataframe dataframe)
{
    Debug.Log($"Name: {dataframe.Name} Age: {dataframe.Age}");
}
```

На сервер все тоже самое. За исключением того
что в методе обработчике будет Id клиента:

```c#
private void TestDataframeHandler(TestDataframe dataframe, int id)
{
    Debug.Log($"Client id: {id} Name: {dataframe.Name} Age:     {dataframe.Age}");
}
```

## 📖 Sending a collection in a dataframe

Сначала нужно создать структуру, которая будет является
элементом коллекции. Нужно реализовать интерфейсы `IWriteable` и `IReadable`.

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

Далее нужно определить датафрейм с коллекцией, например типа List
следующим образом:
```c#
public struct UsersDataframe : INetworkDataframe
{
    public List<UserNetworkModel> Users;

    public void Write(NetFrameWriter writer)
    {
        writer.WriteInt(Users?.Count ?? 0);

       if (Users != null)
       {
           foreach (var user in Users)
           {
               writer.Write(user);
           }
       }
    }


    public void Read(NetFrameReader reader)
    {
        var count = reader.ReadInt();

       if (count > 0)
       {
           Users = new List<UserNetworkModel>();
           for (var i = 0; i < count; i++)
           {
               Users.Add(reader.Read<UserNetworkModel>());
           }
       }
    }
}
```

По этой же аналогии можно реализовать датафреймы
с коллекциями Dictionary и любых других типов.