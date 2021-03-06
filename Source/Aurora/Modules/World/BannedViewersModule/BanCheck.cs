/*
 * Copyright 2011 Matthew Beardmore
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Aurora-Sim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using Aurora.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Nini.Config;
using Aurora.DataManager;
using OpenMetaverse;
using OpenSim.Services.Interfaces;

namespace Aurora.Modules.Ban
{
    #region Grid BanCheck

    public class LoginBanCheck : ILoginModule
    {
        #region Declares

        ILoginService m_service;
        IConfigSource m_source;
        BanCheck m_module;

        #endregion

        #region ILoginModule Members

        public void Initialize(ILoginService service, IConfigSource source, IUserAccountService UASerivce)
        {
            m_source = source;
            m_service = service;
            m_module = new BanCheck(source, UASerivce);
        }

        public bool Login(Hashtable request, UUID User, out string message)
        {
            string ip = (string)request["ip"];
            if (ip == null)
                ip = "";
            string version = (string)request["version"];
            if (version == null)
                version = "";
            string platform = (string)request["platform"];
            if (platform == null)
                platform = "";
            string mac = (string)request["mac"];
            if (mac == null)
                mac = "";
            string id0 = (string)request["id0"];
            if (id0 == null)
                id0 = "";
            return m_module.CheckUser(User, ip,
                version,
                platform,
                mac,
                id0, out message);
        }

        #endregion
    }

    #endregion

    #region BanCheck base

    public class BanCheck
    {
        #region Declares

        private IPresenceInfo presenceInfo = null;

        private AllowLevel GrieferAllowLevel = AllowLevel.AllowCleanOnly;
        private IUserAccountService m_accountService = null;
        private List<string> m_bannedViewers = new List<string>();
        private List<string> m_allowedViewers = new List<string>();
        private bool m_useIncludeList = false;
        private bool m_debug = false;
        private bool m_checkOnLogin = false;
        private bool m_checkOnTimer = true;
        private int TimerMinutes = 60;
        private bool m_enabled = false;

        #endregion

        #region Enums

        public enum AllowLevel : int
        {
            AllowCleanOnly = 0,
            AllowSuspected = 1,
            AllowKnown = 2
        }

        #endregion

        #region Constructor

        public BanCheck (IConfigSource source, IUserAccountService UserAccountService)
        {
            IConfig config = source.Configs["GrieferProtection"];
            if (config == null)
                return;

            m_enabled = config.GetBoolean ("Enabled", true);

            if (!m_enabled)
                return;

            string bannedViewers = config.GetString("ViewersToBan", "");
            m_bannedViewers = Util.ConvertToList(bannedViewers);
            string allowedViewers = config.GetString("ViewersToAllow", "");
            m_allowedViewers = Util.ConvertToList(allowedViewers);
            m_useIncludeList = config.GetBoolean("UseAllowListInsteadOfBanList", false);

            m_checkOnLogin = config.GetBoolean ("CheckForSimilaritiesOnLogin", m_checkOnLogin);
            m_checkOnTimer = config.GetBoolean ("CheckForSimilaritiesOnTimer", m_checkOnTimer);
            TimerMinutes = config.GetInt ("MinutesForTimerToCheck", TimerMinutes);

            if (m_checkOnTimer)
            {
                System.Timers.Timer timer = new System.Timers.Timer (TimerMinutes * 1000 * 60);
                timer.Elapsed += new System.Timers.ElapsedEventHandler (CheckOnTimer);
                timer.Start ();
            }

            GrieferAllowLevel = (AllowLevel)Enum.Parse (typeof (AllowLevel), config.GetString ("GrieferAllowLevel", "AllowKnown"));

            presenceInfo = Aurora.DataManager.DataManager.RequestPlugin<IPresenceInfo> ();
            m_accountService = UserAccountService;

            if (MainConsole.Instance != null)
            {
                MainConsole.Instance.Commands.AddCommand(
                    "UserInfo", "UserInfo [UUID] or [First] [Last]", "Info on a given user", UserInfo);
                MainConsole.Instance.Commands.AddCommand(
                    "SetUserInfo", "SetUserInfo [UUID] or [First] [Last] [Flags]", "Sets the info of the given user [Flags]: Clean, Suspected, Known, Banned", SetUserInfo);
                MainConsole.Instance.Commands.AddCommand(
                    "block user", "block [UUID] or [Name]", "Blocks a given user from connecting anymore", BlockUser);
                MainConsole.Instance.Commands.AddCommand(
                    "unblock user", "unblock [UUID] or [Name]", "Removes the block for logging in on a given user", UnBlockUser);
            }
        }

        #endregion

        #region Private and Protected members

        void CheckOnTimer(object sender, System.Timers.ElapsedEventArgs e)
        {
            presenceInfo.Check(m_useIncludeList ? m_allowedViewers : m_bannedViewers, m_useIncludeList);
        }

        private void CheckForSimilarities(PresenceInfo info)
        {
            presenceInfo.Check(info, m_useIncludeList ? m_allowedViewers : m_bannedViewers, m_useIncludeList);
        }

        private PresenceInfo UpdatePresenceInfo(UUID AgentID, PresenceInfo oldInfo, string ip, string version, string platform, string mac, string id0)
        {
            PresenceInfo info = new PresenceInfo();
            info.AgentID = AgentID;
            info.LastKnownIP = ip;
            info.LastKnownViewer = version;
            info.Platform = platform;
            info.LastKnownMac = mac;
            info.LastKnownID0 = id0;

            if (!oldInfo.KnownID0s.Contains(info.LastKnownID0))
                oldInfo.KnownID0s.Add(info.LastKnownID0);
            if (!oldInfo.KnownIPs.Contains(info.LastKnownIP))
                oldInfo.KnownIPs.Add(info.LastKnownIP);
            if (!oldInfo.KnownMacs.Contains(info.LastKnownMac))
                oldInfo.KnownMacs.Add(info.LastKnownMac);
            if (!oldInfo.KnownViewers.Contains(info.LastKnownViewer))
                oldInfo.KnownViewers.Add(info.LastKnownViewer);

            info.KnownViewers = oldInfo.KnownViewers;
            info.KnownMacs = oldInfo.KnownMacs;
            info.KnownIPs = oldInfo.KnownIPs;
            info.KnownID0s = oldInfo.KnownID0s;
            info.KnownAlts = oldInfo.KnownAlts;

            info.Flags = oldInfo.Flags;

            presenceInfo.UpdatePresenceInfo(info);

            return info;
        }

        private PresenceInfo GetInformation(UUID AgentID)
        {
            PresenceInfo oldInfo = presenceInfo.GetPresenceInfo(AgentID);
            if (oldInfo == null)
            {
                PresenceInfo info = new PresenceInfo();
                info.AgentID = AgentID;
                info.Flags = PresenceInfo.PresenceInfoFlags.Clean;
                presenceInfo.UpdatePresenceInfo(info);
                oldInfo = presenceInfo.GetPresenceInfo(AgentID);
            }

            return oldInfo;
        }

        protected void UserInfo(string[] cmdparams)
        {
            UUID AgentID;
            PresenceInfo info;
            if (!UUID.TryParse(cmdparams[1], out AgentID))
            {
                UserAccount account = m_accountService.GetUserAccount(UUID.Zero, cmdparams[1], cmdparams[2]);
                if (account == null)
                {
                    MainConsole.Instance.Warn("Cannot find user.");
                    return;
                }
                AgentID = account.PrincipalID;
            }
            info = GetInformation(AgentID);
            if (info == null)
            {
                MainConsole.Instance.Warn("Cannot find user.");
                return;
            }
            DisplayUserInfo(info);
        }

        protected void BlockUser(string[] cmdparams)
        {
            UUID AgentID;
            PresenceInfo info;
            if (!UUID.TryParse(cmdparams[2], out AgentID))
            {
                UserAccount account = m_accountService.GetUserAccount(UUID.Zero, Util.CombineParams(cmdparams, 2));
                if (account == null)
                {
                    MainConsole.Instance.Warn("Cannot find user.");
                    return;
                }
                AgentID = account.PrincipalID;
            }
            info = GetInformation(AgentID);
            if (info == null)
            {
                MainConsole.Instance.Warn("Cannot find user.");
                return;
            }
            info.Flags = PresenceInfo.PresenceInfoFlags.Banned;
            presenceInfo.UpdatePresenceInfo(info);
            MainConsole.Instance.Fatal("User blocked from logging in");
        }

        protected void UnBlockUser(string[] cmdparams)
        {
            UUID AgentID;
            PresenceInfo info;
            if (!UUID.TryParse(cmdparams[2], out AgentID))
            {
                UserAccount account = m_accountService.GetUserAccount(UUID.Zero, Util.CombineParams(cmdparams, 2));
                if (account == null)
                {
                    MainConsole.Instance.Warn("Cannot find user.");
                    return;
                }
                AgentID = account.PrincipalID;
            }
            info = GetInformation(AgentID);
            if (info == null)
            {
                MainConsole.Instance.Warn("Cannot find user.");
                return;
            }
            info.Flags = PresenceInfo.PresenceInfoFlags.Clean;
            presenceInfo.UpdatePresenceInfo(info);
            MainConsole.Instance.Fatal("User block removed");
        }

        protected void SetUserInfo(string[] cmdparams)
        {
            UUID AgentID;
            PresenceInfo info;
            int Num = 2;
            if (!UUID.TryParse(cmdparams[1], out AgentID))
            {
                UserAccount account = m_accountService.GetUserAccount(UUID.Zero, cmdparams[1], cmdparams[2]);
                if (account == null)
                {
                    MainConsole.Instance.Warn("Cannot find user.");
                    return;
                }
                AgentID = account.PrincipalID;
                Num += 1;
            }
            info = GetInformation(AgentID);
            if (info == null)
            {
                MainConsole.Instance.Warn("Cannot find user.");
                return;
            }
            try
            {
                info.Flags = (PresenceInfo.PresenceInfoFlags)Enum.Parse(typeof(PresenceInfo.PresenceInfoFlags), cmdparams[Num]);
            }
            catch
            {
                MainConsole.Instance.Warn("Please choose a valid flag: Clean, Suspected, Known, Banned");
                return;
            }
            MainConsole.Instance.Info("Set Flags for " + info.AgentID.ToString() + " to " + info.Flags.ToString());
            presenceInfo.UpdatePresenceInfo(info);
        }

        private void DisplayUserInfo(PresenceInfo info)
        {
            MainConsole.Instance.Info("User Info for " + info.AgentID);
            MainConsole.Instance.Info("   AgentID: " + info.AgentID);
            MainConsole.Instance.Info("   Flags: " + info.Flags);
            /*MainConsole.Instance.Info("   ID0: " + info.LastKnownID0);
            MainConsole.Instance.Info("   IP: " + info.LastKnownIP);
            MainConsole.Instance.Info("   Mac: " + info.LastKnownMac);
            MainConsole.Instance.Info("   Viewer: " + info.LastKnownViewer);
            MainConsole.Instance.Info("   Platform: " + info.Platform);*/
        }

        private bool CheckClient(UUID AgentID, out string message)
        {
            message = "";

            IAgentInfo data = DataManager.DataManager.RequestPlugin<IAgentConnector>().GetAgent(AgentID);
            if (data != null && ((data.Flags & IAgentFlags.PermBan) == IAgentFlags.PermBan || (data.Flags & IAgentFlags.TempBan) == IAgentFlags.TempBan))
            {
                message = "User is banned from the grid.";
                return false;
            }
            PresenceInfo info = GetInformation(AgentID);

            if (m_checkOnLogin)
                CheckForSimilarities(info);

            if (!CheckThreatLevel(info, out message))
                return false;

            return CheckViewer(info, out message);
        }

        private bool CheckViewer(PresenceInfo info, out string reason)
        {
            //Check for banned viewers
            if (IsViewerBanned(info.LastKnownViewer))
            {
                reason = "Viewer is banned";
                return false;
            }
            //Overkill, and perm-bans people who only log in with a bad viewer once
            //foreach (string mac in info.KnownMacs)
            {
                if (info.LastKnownMac.Contains("000"))
                {
                    //Ban this asshole
                    reason = "Viewer is blocked (MAC)";
                    return false;
                }
                if (info.LastKnownMac.Length != 32)
                {
                    reason = "Viewer is blocked (MAC)";
                    return false; //Not a valid length!
                }
            }
            //foreach (string id0 in info.KnownID0s)
            {
                if (info.LastKnownID0.Contains("000"))
                {
                    //Ban this asshole
                    reason = "Viewer is blocked (IO)";
                    return false;
                }
                if (info.LastKnownID0.Length != 32)
                {
                    reason = "Viewer is blocked (IO)";
                    return false; //Valid length!
                }
            }

            reason = "";
            return true;
        }

        public bool IsViewerBanned(string name)
        {
            if (m_useIncludeList)
            {
                if (!m_allowedViewers.Contains(name))
                    return true;
            }
            else
            {
                if (m_bannedViewers.Contains(name))
                    return true;
            }
            return false;
        }

        private bool CheckThreatLevel(PresenceInfo info, out string message)
        {
            message = "";
            if ((info.Flags & PresenceInfo.PresenceInfoFlags.Banned) == PresenceInfo.PresenceInfoFlags.Banned)
            {
                message = "Banned agent.";
                return false;
            }
            if (GrieferAllowLevel == AllowLevel.AllowKnown)
                return true; //Allow all
            else if (GrieferAllowLevel == AllowLevel.AllowCleanOnly)
            { 
                //Allow people with only clean flag or suspected alt
                if ((info.Flags & PresenceInfo.PresenceInfoFlags.Clean) == PresenceInfo.PresenceInfoFlags.Clean)
                    return true;
                else
                {
                    message = "Not a Clean agent and have been denied access.";
                    return false;
                }
            }
            else if (GrieferAllowLevel == AllowLevel.AllowSuspected)
            {
                //Block all alts of knowns, and suspected alts of knowns
                if ((info.Flags & PresenceInfo.PresenceInfoFlags.Known) == PresenceInfo.PresenceInfoFlags.Known ||
                    (info.Flags & PresenceInfo.PresenceInfoFlags.SuspectedAltAccountOfKnown) == PresenceInfo.PresenceInfoFlags.SuspectedAltAccountOfKnown || 
                    (info.Flags & PresenceInfo.PresenceInfoFlags.KnownAltAccountOfKnown) == PresenceInfo.PresenceInfoFlags.KnownAltAccountOfKnown)
                {
                    message = "Not a Clean agent and have been denied access.";
                    return false;
                }
                else
                    return true;
            }

            return true;
        }

        #endregion

        #region Public members

        public bool CheckUser(UUID AgentID, string ip, string version, string platform, string mac, string id0, out string message)
        {
            message = "";
            if (!m_enabled)
                return true;

            PresenceInfo oldInfo = GetInformation(AgentID);
            oldInfo = UpdatePresenceInfo(AgentID, oldInfo, ip, version, platform, mac, id0);
            if (m_debug)
                DisplayUserInfo(oldInfo);

            return CheckClient(AgentID, out message);
        }

        public void SetUserLevel(UUID AgentID, PresenceInfo.PresenceInfoFlags presenceInfoFlags)
        {
            if (!m_enabled)
                return;
            //Get
            PresenceInfo info = GetInformation(AgentID);
            //Set the flags
            info.Flags = presenceInfoFlags;
            //Save
            presenceInfo.UpdatePresenceInfo(info);
        }

        #endregion
    }

    #endregion

    #region IP block check

    public class IPBanCheck : ILoginModule
    {
        #region Declares

        private ILoginService m_service;
        private IConfigSource m_source;
        private List<IPAddress> IPBans = new List<IPAddress>();
        private List<string> IPRangeBans = new List<string>();

        #endregion

        #region ILoginModule Members

        public void Initialize(ILoginService service, IConfigSource source, IUserAccountService UASerivce)
        {
            m_source = source;
            m_service = service;

            IConfig config = source.Configs["GrieferProtection"];
            if (config != null)
            {
                List<string> iPBans = Util.ConvertToList(config.GetString("IPBans", ""));
                foreach (string ip in iPBans)
                {
                    IPAddress ipa;
                    if(IPAddress.TryParse(ip, out ipa))
                        IPBans.Add(ipa);
                }
                IPRangeBans = Util.ConvertToList(config.GetString("IPRangeBans", ""));
            }
        }

        public bool Login(Hashtable request, UUID User, out string message)
        {
            message = "";
            string ip = (string)request["ip"];
            if (ip == null)
                ip = "";
            ip = ip.Split(':')[0];//Remove the port
            IPAddress userIP = IPAddress.Parse(ip);
            if (IPBans.Contains(userIP))
                return false;
            foreach (string ipRange in IPRangeBans)
            {
                string[] split = ipRange.Split('-');
                if (split.Length != 2)
                    continue;
                IPAddress low = IPAddress.Parse(ip);
                IPAddress high = IPAddress.Parse(ip);
                NetworkUtils.IPAddressRange range = new NetworkUtils.IPAddressRange(low, high);
                if (range.IsInRange(userIP))
                    return false;
            }
            return true;
        }

        #endregion
    }

    #endregion
}
