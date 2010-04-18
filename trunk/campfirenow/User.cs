﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Flare
{
    /// <summary>
    /// The current user's login details and Flare preferences
    /// </summary>
    public class User
    {
        #region Delegates

        public delegate void UpdateProgressTextCallback(String text);

        #endregion

        private readonly WaitCallback uploadFileCalback;
        private String nickname;

        public User()
        {
            uploadFileCalback = new WaitCallback(UploadFileCallback);
        }

        public String Username { get; set; }
        public String Password { get; set; }
        public Int32 NotifyWindowDelay { get; set; }
        public bool UseOpenId { get; set; }
        public string OpenIdUrl { get; set; }
        public bool MinimiseDuringStartup { get; set; }
        public bool MinimiseInsteadOfQuitting { get; set; }

        public String Nickname
        {
            get
            {
                if (String.IsNullOrEmpty(nickname))
                    return Username.Contains("@") ? Username.Substring(0, Username.IndexOf('@')) : Username;
                return nickname;
            }
            set { nickname = value; }
        }

        public Boolean LoginAsGuest { get; set; }
        public Boolean ShowMessageNotifications { get; set; }
        public Boolean NotifyOnlyWhenNicknameIsFound { get; set; }
        public List<String> RoomNames { get; set; }
        public UploadDetails LastUploadDetails { get; set; }

        /// <summary>
        /// Retreives the user's details and preferences from the default registry entries.
        /// </summary>
        public static User FromRegistry()
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\Flare");

            var user = new User();

            user.LoginAsGuest = (key.GetValue("loginAsGuest", "0").ToString() == "1");
            user.Username = key.GetValue("username", string.Empty).ToString();
            user.Nickname = key.GetValue("nickname", string.Empty).ToString();
            user.Password = DecryptPassword(key);
            user.OpenIdUrl = key.GetValue("openIdUrl", string.Empty).ToString();
            user.MinimiseDuringStartup = (key.GetValue("minstartup", "0").ToString() == "1");
            user.MinimiseInsteadOfQuitting = (key.GetValue("minquit", "0").ToString() == "1");
            try
            {
                user.NotifyWindowDelay = Int32.Parse(key.GetValue("notifydelay", "1500").ToString());
            }
            catch (FormatException)
            {
                user.NotifyWindowDelay = 1500;
            }
            user.ShowMessageNotifications = (key.GetValue("showMsgNotify", "1").ToString() == "1");
            user.UseOpenId = (key.GetValue("useOpenId", "1").ToString() == "1");
            user.RoomNames = new List<string>();
            string[] rooms = key.GetValue("roomnames", "notset").ToString().Split(',');
            foreach (string room in rooms)
                user.RoomNames.Add(room);
            user.NotifyOnlyWhenNicknameIsFound = key.GetValue("nicknotifications", "0").ToString() == "1";

            key.Close();

            return user;
        }

        private static string DecryptPassword(RegistryKey key)
        {
            var salt = key.GetValue("usersalt", string.Empty).ToString();
            if (string.IsNullOrEmpty(salt))
            {
                // The user is storing their password without encryption, probably from an older version of Flare
                // Encrypt the password for them and save it back
                EncryptAndSavePassword(key, key.GetValue("password", string.Empty).ToString());
                salt = key.GetValue("usersalt", string.Empty).ToString();
            }
            var password = (byte[]) key.GetValue("password", new byte[0]);
            return PasswordProtector.Unprotect(password, salt);
        }

        private static void EncryptAndSavePassword(RegistryKey key, string password)
        {
            var saltGuid = Guid.NewGuid().ToString();
            key.SetValue("usersalt", saltGuid);
            var encryptedPassword = PasswordProtector.Protect(password, saltGuid);
            key.DeleteValue("password", false);
            key.SetValue("password", encryptedPassword, RegistryValueKind.Binary);
        }

        public void Save()
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\Flare");
            key.SetValue("loginAsGuest", LoginAsGuest ? "1" : "0");
            key.SetValue("username", Username);
            key.SetValue("useOpenId", UseOpenId ? "1" : "0");
            if (OpenIdUrl == null)
                OpenIdUrl = string.Empty;
            key.SetValue("openidurl", OpenIdUrl);
            EncryptAndSavePassword(key, Password);
            key.SetValue("nickname", Nickname);
            key.SetValue("minstartup", MinimiseDuringStartup ? "1" : "0");
            key.SetValue("minquit", MinimiseInsteadOfQuitting ? "1" : "0");
            key.SetValue("notifydelay", NotifyWindowDelay.ToString());
            if (RoomNames == null)
                RoomNames = new List<string>();
            key.SetValue("roomnames", string.Join(",", RoomNames.ToArray()));
            key.SetValue("nicknotifications", NotifyOnlyWhenNicknameIsFound ? "1" : "0");
            key.Close();
        }

        public void UploadFileToCurrentRoom(String url, String file, String cookieString, Label progressLabel)
        {
            var ud = new UploadDetails();
            ud.Uri = url;
            ud.FileName = file;
            ud.CookieData = cookieString;
            ud.ProgressLabel = progressLabel;
            ThreadPool.QueueUserWorkItem(uploadFileCalback, ud);
            LastUploadDetails = ud;
        }

        private void UploadFileCallback(object state)
        {
            var ud = (UploadDetails) state;

            // Create a boundry
            String boundary = "----------------------------" + DateTime.Now.Ticks.ToString("x");

            // Create the web request
            var httpWebRequest = (HttpWebRequest) WebRequest.Create(ud.Uri);
            httpWebRequest.ContentType = "multipart/form-data; boundary=" + boundary;
            httpWebRequest.Method = "POST";
            httpWebRequest.Accept =
                "image/gif, image/x-xbitmap, image/jpeg, image/pjpeg, application/x-ms-application, application/vnd.ms-xpsdocument, application/xaml+xml, application/x-ms-xbap, application/vnd.ms-excel, application/vnd.ms-powerpoint, application/msword, application/x-shockwave-flash, application/x-silverlight, */*";
            httpWebRequest.Headers.Add("Accept-Encoding", "gzip, deflate");
            httpWebRequest.Headers.Add("Accept-Language", "en-us");
            httpWebRequest.Headers.Add("UA-CPU", "x86");
            httpWebRequest.Referer = "http://mattbrindley.campfirenow.com/room/36735";
            httpWebRequest.CookieContainer = new CookieContainer();
            httpWebRequest.CookieContainer.SetCookies(new Uri(ud.Uri), ud.CookieData);

            httpWebRequest.KeepAlive = true;
            httpWebRequest.Credentials =
                CredentialCache.DefaultCredentials;

            // Get the boundry in bytes
            Byte[] boundarybytes = Encoding.ASCII.GetBytes("--" + boundary + "\r\n");

            // Get the header for the file upload
            String headerTemplate =
                "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: application/octet-stream\r\n\r\n";

            // Add the filename to the header
            String header = String.Format(headerTemplate, "upload", ud.FileName);

            //convert the header to a byte array
            Byte[] headerbytes = Encoding.UTF8.GetBytes(header);

            // Add all of the content up.
            httpWebRequest.ContentLength = new FileInfo(ud.FileName).Length + headerbytes.Length +
                                           (boundarybytes.Length*2) + 4;

            // Get the output stream
            Stream requestStream = httpWebRequest.GetRequestStream();

            // Write out the starting boundry
            requestStream.Write(boundarybytes, 0, boundarybytes.Length);

            // Write the header including the filename.
            requestStream.Write(headerbytes, 0, headerbytes.Length);

            // Open up a filestream.
            var fileStream = new FileStream(ud.FileName, FileMode.Open, FileAccess.Read);

            // Use 4096 for the buffer
            var buffer = new Byte[4096];
            Int64 fileTotalLengthInKb = new FileInfo(ud.FileName).Length/1024;
            Int64 totalBytesRead = 0;
            Int32 bytesRead = 0;
            // Loop through whole file uploading parts in a stream.
            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
            {
                ud.ProgressLabel.Invoke(new UpdateProgressTextCallback(UpdateProgressText),
                                        String.Format("Uploading...\n{0:n}/{1:n}KB", totalBytesRead/1024,
                                                      fileTotalLengthInKb));
                requestStream.Write(buffer, 0, bytesRead);
                requestStream.Flush();
                totalBytesRead += 4096;
            }

            boundarybytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");

            // Write out the trailing boundry
            requestStream.Write(boundarybytes, 0, boundarybytes.Length);

            // Close the request and file stream
            requestStream.Close();
            fileStream.Close();
            httpWebRequest.GetResponse();
        }

        public void UpdateProgressText(String text)
        {
            LastUploadDetails.ProgressLabel.Text = text;
        }
    }

    // Although it would make more sense for this to be a struct, it's used primarily as a WaitCallback's 'state' param, which is of type object
    // so, as a value type, it would get boxed and unboxed all over the place, it's a performance nightmare. Best to leave it as a class.
    public class UploadDetails
    {
        public String Uri { get; set; }
        public String FileName { get; set; }
        public String CookieData { get; set; }
        public Label ProgressLabel { get; set; }
    }
}