using System.Collections.Generic;
using NetFrame;
using NetFrame.WriteAndRead;
using Samples.Datagrams.Collections;

namespace Samples.Datagrams
{
    public struct UsersDatagram : INetFrameDatagram
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
}