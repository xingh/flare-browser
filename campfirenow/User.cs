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
      }

      public String Token { get; set; }
      public String RefreshToken { get; set; }
      public String Username { get; set; }
      public Int32 NotifyWindowDelay { get; set; }
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
         user.Token = key.GetValue("token", string.Empty).ToString();
         user.RefreshToken = key.GetValue("refreshtoken", string.Empty).ToString();
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
         user.RoomNames = new List<string>();
         string[] rooms = key.GetValue("roomnames", "notset").ToString().Split(',');
         foreach (string room in rooms)
            user.RoomNames.Add(room);
         user.NotifyOnlyWhenNicknameIsFound = key.GetValue("nicknotifications", "0").ToString() == "1";

         key.Close();

         return user;
      }

      public void Save()
      {
         RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\Flare");
         key.SetValue("loginAsGuest", LoginAsGuest ? "1" : "0");
         key.SetValue("username", Username);
         key.SetValue("token", Token);
         key.SetValue("refreshtoken", RefreshToken);
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
   }
}