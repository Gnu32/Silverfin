/*
 * Copyright (c) Contributors, http://aurora-sim.org/, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using Aurora.Simulation.Base;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Aurora.Framework;
using OpenSim.Services.Interfaces;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;

namespace OpenSim.Services.Connectors.SimianGrid
{
    /// <summary>
    ///   Stores and retrieves friend lists from the SimianGrid backend
    /// </summary>
    public class SimianFriendsServiceConnector : IFriendsService, IService
    {
        private string m_serverUrl = String.Empty;

        public string Name
        {
            get { return GetType().Name; }
        }

        #region IFriendsService Members

        public virtual IFriendsService InnerService
        {
            get { return this; }
        }

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
        }


        public void FinishedStartup()
        {
        }

        #endregion

        public void PostInitialize(IConfigSource config, IRegistryCore registry)
        {
            IConfig handlerConfig = config.Configs["Handlers"];
            if (handlerConfig.GetString("FriendsHandler", "") != Name)
                return;

            CommonInit(config);
            registry.RegisterModuleInterface<IFriendsService>(this);
        }

        public void AddNewRegistry(IConfigSource config, IRegistryCore registry)
        {
            IConfig handlerConfig = config.Configs["Handlers"];
            if (handlerConfig.GetString("FriendsHandler", "") != Name)
                return;

            registry.RegisterModuleInterface<IFriendsService>(this);
        }

        private void CommonInit(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["FriendsService"];
            if (gridConfig != null)
            {
                string serviceUrl = gridConfig.GetString("FriendsServerURI");
                if (!String.IsNullOrEmpty(serviceUrl))
                {
                    if (!serviceUrl.EndsWith("/") && !serviceUrl.EndsWith("="))
                        serviceUrl = serviceUrl + '/';
                    m_serverUrl = serviceUrl;
                }
            }

            if (String.IsNullOrEmpty(m_serverUrl))
                MainConsole.Instance.Info("[SIMIAN FRIENDS CONNECTOR]: No FriendsServerURI specified, disabling connector");
        }

        private OSDArray GetFriended(UUID ownerID)
        {
            NameValueCollection requestArgs = new NameValueCollection
                                                  {
                                                      {"RequestMethod", "GetGenerics"},
                                                      {"OwnerID", ownerID.ToString()},
                                                      {"Type", "Friend"}
                                                  };

            OSDMap response = WebUtils.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean() && response["Entries"] is OSDArray)
            {
                return (OSDArray) response["Entries"];
            }
            else
            {
                MainConsole.Instance.Warn("[SIMIAN FRIENDS CONNECTOR]: Failed to retrieve friends for user " + ownerID + ": " +
                           response["Message"].AsString());
                return new OSDArray(0);
            }
        }

        private OSDArray GetFriendedBy(UUID ownerID)
        {
            NameValueCollection requestArgs = new NameValueCollection
                                                  {
                                                      {"RequestMethod", "GetGenerics"},
                                                      {"Key", ownerID.ToString()},
                                                      {"Type", "Friend"}
                                                  };

            OSDMap response = WebUtils.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean() && response["Entries"] is OSDArray)
            {
                return (OSDArray) response["Entries"];
            }
            else
            {
                MainConsole.Instance.Warn("[SIMIAN FRIENDS CONNECTOR]: Failed to retrieve reverse friends for user " + ownerID + ": " +
                           response["Message"].AsString());
                return new OSDArray(0);
            }
        }

        #region IFriendsService

        public List<FriendInfo> GetFriends(UUID principalID)
        {
            if (String.IsNullOrEmpty(m_serverUrl))
                return new List<FriendInfo>();

            Dictionary<UUID, FriendInfo> friends = new Dictionary<UUID, FriendInfo>();

            OSDArray friendsArray = GetFriended(principalID);
            OSDArray friendedMeArray = GetFriendedBy(principalID);

            // Load the list of friends and their granted permissions
            foreach (OSD t in friendsArray)
            {
                OSDMap friendEntry = t as OSDMap;
                if (friendEntry != null)
                {
                    UUID friendID = friendEntry["Key"].AsUUID();

                    FriendInfo friend = new FriendInfo
                                            {
                                                PrincipalID = principalID,
                                                Friend = friendID.ToString(),
                                                MyFlags = friendEntry["Value"].AsInteger(),
                                                TheirFlags = -1
                                            };

                    friends[friendID] = friend;
                }
            }

            // Load the permissions those friends have granted to this user
            foreach (OSD t in friendedMeArray)
            {
                OSDMap friendedMeEntry = t as OSDMap;
                if (friendedMeEntry != null)
                {
                    UUID friendID = friendedMeEntry["OwnerID"].AsUUID();

                    FriendInfo friend;
                    if (friends.TryGetValue(friendID, out friend))
                        friend.TheirFlags = friendedMeEntry["Value"].AsInteger();
                }
            }

            // Convert the dictionary of friends to an array and return it
            List<FriendInfo> array = new List<FriendInfo>();
            foreach (FriendInfo friend in friends.Values)
                array.Add(friend);

            return array;
        }

        public bool StoreFriend(UUID principalID, string friend, int flags)
        {
            if (String.IsNullOrEmpty(m_serverUrl))
                return true;

            NameValueCollection requestArgs = new NameValueCollection
                                                  {
                                                      {"RequestMethod", "AddGeneric"},
                                                      {"OwnerID", principalID.ToString()},
                                                      {"Type", "Friend"},
                                                      {"Key", friend},
                                                      {"Value", flags.ToString()}
                                                  };

            OSDMap response = WebUtils.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                MainConsole.Instance.Error("[SIMIAN FRIENDS CONNECTOR]: Failed to store friend " + friend + " for user " + principalID +
                            ": " + response["Message"].AsString());

            return success;
        }

        public bool Delete(UUID principalID, string friend)
        {
            if (String.IsNullOrEmpty(m_serverUrl))
                return true;

            NameValueCollection requestArgs = new NameValueCollection
                                                  {
                                                      {"RequestMethod", "RemoveGeneric"},
                                                      {"OwnerID", principalID.ToString()},
                                                      {"Type", "Friend"},
                                                      {"Key", friend}
                                                  };

            OSDMap response = WebUtils.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                MainConsole.Instance.Error("[SIMIAN FRIENDS CONNECTOR]: Failed to remove friend " + friend + " for user " + principalID +
                            ": " + response["Message"].AsString());

            return success;
        }

        #endregion IFriendsService
    }
}