using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace NetFrame.Encryption
{
    public class NetFrameCryptographer
    {
        private UnicodeEncoding _byteConverter;
        
        public NetFrameCryptographer()
        {
            _byteConverter = new UnicodeEncoding();
        }
        
        public void TestRun()
        {
            //массивы байтов для хранения исходных, зашифрованных и расшифрованных данных.
            byte[] dataToEncrypt = _byteConverter.GetBytes("Data to Encrypt");
            byte[] encryptedData;
            byte[] decryptedData;

                
            //Создаем новый экземпляр RSACryptoServiceProvider для генерации
            //данные открытого и закрытого ключей.
            using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
            {
                   
                //Передаем данные в Encrypt, информацию об открытом ключе 
                //(с использованием RSACryptoServiceProvider.ExportParameters(false),
                //и логический флаг, указывающий отсутствие заполнения OAEP.
                encryptedData = Encrypt(dataToEncrypt, RSA.ExportParameters(false), false);
                Debug.Log($"Encrypt plaintext: {_byteConverter.GetString(encryptedData)}");
                    
                //Передаем данные в Decrypt, информацию о закрытом ключе 
                //(с использованием RSACryptoServiceProvider.ExportParameters(true),
                //и логический флаг, указывающий отсутствие заполнения OAEP.
                decryptedData = Decrypt(encryptedData, RSA.ExportParameters(true), false);
                Debug.Log($"Decrypted plaintext: {_byteConverter.GetString(decryptedData)}");
            }
        }
        
        private byte[] Encrypt(byte[] DataToEncrypt, RSAParameters RSAKeyInfo, bool DoOAEPPadding)
        {
            byte[] encryptedData;
            using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
            {

                //Импортируем информацию о ключе RSA. Для этого нужно только
                //для включения информации об открытом ключе.
                RSA.ImportParameters(RSAKeyInfo);

                //Шифрование переданного массива байтов и указание заполнения OAEP.  
                //Заполнение OAEP доступно только в Microsoft Windows XP или
                //позже.
                encryptedData = RSA.Encrypt(DataToEncrypt, DoOAEPPadding);
            }
            return encryptedData;
        }
        
        private static byte[] Decrypt(byte[] DataToDecrypt, RSAParameters RSAKeyInfo, bool DoOAEPPadding)
        {
            byte[] decryptedData;
            //Create a new instance of RSACryptoServiceProvider.
            using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
            {
                //Импортируем информацию о ключе RSA. Это требует
                //чтобы включить информацию о закрытом ключе.
                RSA.ImportParameters(RSAKeyInfo);

                //Расшифровываем переданный массив байтов и указываем заполнение OAEP.  
                //Заполнение OAEP доступно только в Microsoft Windows XP или
                //позже.
                decryptedData = RSA.Decrypt(DataToDecrypt, DoOAEPPadding);
            }
            return decryptedData;
        }

        //В следующем примере кода сведения о ключе, созданные RSACryptoServiceProvider с помощью,
        //экспортируются в RSAParameters объект.
        private void Export()
        {
            //Create a new RSACryptoServiceProvider object.
            using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
            {
                //Экспортируем информацию о ключах в объект RSAParameters.
                //Передайте false, чтобы экспортировать информацию об открытом ключе, или передайте
                //true для экспорта информации об открытом и закрытом ключе.
                RSAParameters parameters = RSA.ExportParameters(false);
            }
        }
    }
}