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
using System.Linq;
using System.Reflection;
using Aurora.DataManager;
using Aurora.Framework;
using Aurora.Simulation.Base;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using RegionFlags = Aurora.Framework.RegionFlags;

namespace OpenSim.Services.GridService
{
    public class GridService : IGridService, IService
    {
        private static GridService m_RootInstance;
        protected bool m_AllowDuplicateNames;
        protected bool m_AllowNewRegistrations = true;
        protected IAuthenticationService m_AuthenticationService;
        protected IRegionData m_Database;
        private bool m_DeleteOnUnregister = true;
        protected bool m_DisableRegistrations;
        protected int m_RegionViewSize = 256;
        protected bool m_UseSessionID = true;
        protected IConfigSource m_config;
        protected int m_maxRegionSize;
        protected IRegistryCore m_registryCore;
        protected ISimulationBase m_simulationBase;

        public virtual string Name
        {
            get { return GetType().Name; }
        }

        #region IGridService Members

        public int MaxRegionSize
        {
            get { return m_maxRegionSize; }
        }

        public int RegionViewSize
        {
            get { return m_RegionViewSize; }
        }

        public virtual IGridService InnerService
        {
            get { return this; }
        }

        public virtual void Configure(IConfigSource config, IRegistryCore registry)
        {
            m_config = config;
            IConfig gridConfig = config.Configs["GridService"];
            if (gridConfig != null)
            {
                m_DisableRegistrations = gridConfig.GetBoolean("DisableRegistrations", m_DisableRegistrations);
                m_AllowNewRegistrations = gridConfig.GetBoolean("AllowNewRegistrations", m_AllowNewRegistrations);
                m_DeleteOnUnregister = gridConfig.GetBoolean("DeleteOnUnregister", m_DeleteOnUnregister);
                m_maxRegionSize = gridConfig.GetInt("MaxRegionSize", m_maxRegionSize);
                m_RegionViewSize = gridConfig.GetInt("RegionViewSize", m_RegionViewSize/Constants.RegionSize)*
                                   Constants.RegionSize;
                m_DeleteOnUnregister = gridConfig.GetBoolean("DeleteOnUnregister", m_DeleteOnUnregister);
                m_UseSessionID = !gridConfig.GetBoolean("DisableSessionID", !m_UseSessionID);
                m_AllowDuplicateNames = gridConfig.GetBoolean("AllowDuplicateNames", m_AllowDuplicateNames);
            }

            if (m_RootInstance == null)
            {
                m_RootInstance = this;

                if (MainConsole.Instance != null)
                {
                    MainConsole.Instance.Commands.AddCommand("show region",
                                                             "show region [Region name]",
                                                             "Show details on a region",
                                                             HandleShowRegion);

                    MainConsole.Instance.Commands.AddCommand("set region flags",
                                                             "set region flags [Region name] [flags]",
                                                             "Set database flags for region",
                                                             HandleSetFlags);

                    MainConsole.Instance.Commands.AddCommand("grid clear regions",
                                                             "grid clear regions",
                                                             "Clears all regions from the database",
                                                             HandleClearAllRegions);

                    MainConsole.Instance.Commands.AddCommand("grid clear down regions",
                                                             "grid clear down regions",
                                                             "Clears all regions that are offline from the database",
                                                             HandleClearAllDownRegions);

                    MainConsole.Instance.Commands.AddCommand("grid clear region",
                                                             "grid clear region [RegionName]",
                                                             "Clears the regions with the given name from the database",
                                                             HandleClearRegion);
                }
            }
            registry.RegisterModuleInterface<IGridService>(this);
        }

        public virtual void Start(IConfigSource config, IRegistryCore registry)
        {
            m_registryCore = registry;
            m_AuthenticationService = registry.RequestModuleInterface<IAuthenticationService>();
            m_simulationBase = registry.RequestModuleInterface<ISimulationBase>();
            m_Database = DataManager.RequestPlugin<IRegionData>();

            if (m_Database == null)
                throw new Exception("Could not find a storage interface in the given module");
        }

        public virtual void FinishedStartup()
        {
        }

        public virtual List<GridRegion> GetDefaultRegions(UUID scopeID)
        {
            List<GridRegion> regions = m_Database.GetDefaultRegions(scopeID);

#if (!ISWIN)
            List<GridRegion> ret = new List<GridRegion>();
            foreach (GridRegion r in regions)
            {
                if ((r.Flags & (int) RegionFlags.RegionOnline) != 0) ret.Add(r);
            }
#else
            List<GridRegion> ret = regions.Where(r => (r.Flags & (int)RegionFlags.RegionOnline) != 0).ToList();
#endif

            MainConsole.Instance.DebugFormat("[GRID SERVICE]: GetDefaultRegions returning {0} regions", ret.Count);
            return ret;
        }

        /// <summary>
        ///   Attempts to find regions that are good for the agent to login to if the default and fallback regions are down.
        /// </summary>
        /// <param name = "scopeID"></param>
        /// <param name = "x"></param>
        /// <param name = "y"></param>
        /// <returns></returns>
        public virtual List<GridRegion> GetSafeRegions(UUID scopeID, int x, int y)
        {
            return m_Database.GetSafeRegions(scopeID, x, y);
        }

        /// <summary>
        ///   Tells the grid server that this region is not able to be connected to.
        ///   This updates the down flag in the map and blocks it from becoming a 'safe' region fallback
        ///   Only called by LLLoginService
        /// </summary>
        /// <param name = "r"></param>
        public virtual void SetRegionUnsafe(UUID ID)
        {
            GridRegion data = m_Database.Get(ID, UUID.Zero);
            if (data == null)
                return;
            if ((data.Flags & (int) RegionFlags.Safe) == (int) RegionFlags.Safe)
                data.Flags &= ~(int) RegionFlags.Safe; //Remove only the safe var the first time
            else if ((data.Flags & (int) RegionFlags.RegionOnline) == (int) RegionFlags.RegionOnline)
                data.Flags &= ~(int) RegionFlags.RegionOnline; //Remove online the second time it fails
            m_Database.Store(data);
        }

        /// <summary>
        ///   Tells the grid server that this region is able to be connected to.
        ///   This updates the down flag in the map and allows it to become a 'safe' region fallback
        ///   Only called by LLLoginService
        /// </summary>
        /// <param name = "r"></param>
        public virtual void SetRegionSafe(UUID ID)
        {
            GridRegion data = m_Database.Get(ID, UUID.Zero);
            if (data == null)
                return;
            if ((data.Flags & (int) RegionFlags.Safe) == 0)
                data.Flags |= (int) RegionFlags.Safe;
            else if ((data.Flags & (int) RegionFlags.RegionOnline) == 0)
                data.Flags |= (int) RegionFlags.RegionOnline;
            m_Database.Store(data);
        }

        public virtual List<GridRegion> GetFallbackRegions(UUID scopeID, int x, int y)
        {
            List<GridRegion> regions = m_Database.GetFallbackRegions(scopeID, x, y);

#if (!ISWIN)
            List<GridRegion> ret = new List<GridRegion>();
            foreach (GridRegion r in regions)
            {
                if ((r.Flags & (int) RegionFlags.RegionOnline) != 0) ret.Add(r);
            }
#else
            List<GridRegion> ret = regions.Where(r => (r.Flags & (int) RegionFlags.RegionOnline) != 0).ToList();
#endif

            MainConsole.Instance.DebugFormat("[GRID SERVICE]: Fallback returned {0} regions", ret.Count);
            return ret;
        }

        public virtual int GetRegionFlags(UUID scopeID, UUID regionID)
        {
            GridRegion region = m_Database.Get(regionID, scopeID);

            if (region != null)
            {
                //MainConsole.Instance.DebugFormat("[GRID SERVICE]: Request for flags of {0}: {1}", regionID, flags);
                return region.Flags;
            }
            else
                return -1;
        }

        public virtual multipleMapItemReply GetMapItems(ulong regionHandle, GridItemType gridItemType)
        {
            multipleMapItemReply allItems = new multipleMapItemReply();
            if (gridItemType == GridItemType.AgentLocations) //Grid server only cares about agent locations
            {
                int X, Y;
                Util.UlongToInts(regionHandle, out X, out Y);
                //Get the items and send them back
                allItems.items[regionHandle] = GetItems(X, Y, regionHandle);
            }
            return allItems;
        }

        #endregion

        #region IGridService

        public virtual string RegisterRegion(GridRegion regionInfos, UUID oldSessionID, out UUID SessionID,
                                             out List<GridRegion> neighbors)
        {
            SessionID = UUID.Zero;
            neighbors = new List<GridRegion>();
            if (m_DisableRegistrations)
                return "Registrations are disabled.";

            UUID NeedToDeletePreviousRegion = UUID.Zero;

            IConfig gridConfig = m_config.Configs["GridService"];

            //Get the range of this so that we get the full count and make sure that we are not overlapping smaller regions
            List<GridRegion> regions = m_Database.Get(regionInfos.RegionLocX, regionInfos.RegionLocY,
                                                      regionInfos.RegionLocX + regionInfos.RegionSizeX - 1,
                                                      regionInfos.RegionLocY + regionInfos.RegionSizeY - 1,
                                                      regionInfos.ScopeID);

            if (regions.Count > 1)
            {
                //More than one region is here... it is overlapping stuff
                MainConsole.Instance.WarnFormat(
                    "[GRID SERVICE]: Region {0} tried to register in coordinates {1}, {2} which are already in use in scope {3}.",
                    regionInfos.RegionID, regionInfos.RegionLocX, regionInfos.RegionLocY, regionInfos.ScopeID);
                return "Region overlaps another region";
            }

            GridRegion region = regions.Count > 0 ? regions[0] : null;

            if (!m_AllowNewRegistrations && region == null)
            {
                MainConsole.Instance.WarnFormat("[GRID SERVICE]: Region {0} tried to register but registrations are disabled.",
                                 regionInfos.RegionName);
                return "Registrations are disabled.";
            }

            if (m_maxRegionSize != 0 &&
                (regionInfos.RegionSizeX > m_maxRegionSize || regionInfos.RegionSizeY > m_maxRegionSize))
            {
                //Too big... kick it out
                MainConsole.Instance.WarnFormat("[GRID SERVICE]: Region {0} tried to register with too large of a size {1},{2}.",
                                 regionInfos.RegionName, regionInfos.RegionSizeX, regionInfos.RegionSizeY);
                return "Region overlaps another region";
            }

            if ((region != null) && (region.RegionID != regionInfos.RegionID))
            {
                MainConsole.Instance.WarnFormat(
                    "[GRID SERVICE]: Region {0} tried to register in coordinates {1}, {2} which are already in use in scope {3}.",
                    regionInfos.RegionName, regionInfos.RegionLocX, regionInfos.RegionLocY, regionInfos.ScopeID);
                return "Region overlaps another region";
            }

            if ((region != null) && (region.RegionID == regionInfos.RegionID) &&
                ((region.RegionLocX != regionInfos.RegionLocX) || (region.RegionLocY != regionInfos.RegionLocY)))
            {
                if ((region.Flags & (int) RegionFlags.NoMove) != 0)
                    return "Can't move this region," + region.RegionLocX + "," + region.RegionLocY;

                // Region reregistering in other coordinates. Delete the old entry
                MainConsole.Instance.DebugFormat(
                    "[GRID SERVICE]: Region {0} ({1}) was previously registered at {2}-{3}. Deleting old entry.",
                    regionInfos.RegionName, regionInfos.RegionID, regionInfos.RegionLocX, regionInfos.RegionLocY);

                NeedToDeletePreviousRegion = regionInfos.RegionID;
            }

            if (region != null)
            {
                // There is a preexisting record
                //
                // Get it's flags
                //
                RegionFlags rflags = (RegionFlags) region.Flags;

                // Is this a reservation?
                //
                if ((rflags & RegionFlags.Reservation) != 0)
                {
                    // Regions reserved for the null key cannot be taken.
                    if (region.SessionID == UUID.Zero)
                        return "Region location is reserved";

                    // Treat it as an auth request
                    //
                    // NOTE: Fudging the flags value here, so these flags
                    //       should not be used elsewhere. Don't optimize
                    //       this with the later retrieval of the same flags!
                    rflags |= RegionFlags.Authenticate;
                }

                if ((rflags & RegionFlags.Authenticate) != 0)
                {
                    // Can we authenticate at all?
                    //
                    if (m_AuthenticationService == null)
                        return "No authentication possible";
                    //Make sure the key exists
                    if (!m_AuthenticationService.CheckExists(regionInfos.SessionID, "SessionID"))
                        return "Bad authentication";
                    //Now verify the key
                    if (!m_AuthenticationService.Verify(regionInfos.SessionID, "SessionID", regionInfos.AuthToken, 30))
                        return "Bad authentication";
                }
            }

            if (!m_AllowDuplicateNames)
            {
                List<GridRegion> dupe = m_Database.Get(regionInfos.RegionName, regionInfos.ScopeID);
                if (dupe != null && dupe.Count > 0)
                {
#if (!ISWIN)
                    foreach (GridRegion d in dupe)
                    {
                        if (d.RegionID != regionInfos.RegionID)
                        {
                            MainConsole.Instance.WarnFormat("[GRID SERVICE]: Region {0} tried to register duplicate name with ID {1}.", regionInfos.RegionName, regionInfos.RegionID);
                            return "Duplicate region name";
                        }
                    }
#else
                    if (dupe.Any(d => d.RegionID != regionInfos.RegionID))
                    {
                        MainConsole.Instance.WarnFormat("[GRID SERVICE]: Region {0} tried to register duplicate name with ID {1}.",
                                         regionInfos.RegionName, regionInfos.RegionID);
                        return "Duplicate region name";
                    }
#endif
                }
            }

            if (region != null)
            {
                //If we are locked out, we can't come in
                if ((region.Flags & (int) RegionFlags.LockedOut) != 0)
                    return "Region locked out";

                //Remove the reservation if we are there now
                region.Flags &= ~(int) RegionFlags.Reservation;

                regionInfos.Flags = region.Flags; // Preserve flags
            }
            else
            {
                //Regions do not get to set flags, so wipe them
                regionInfos.Flags = 0;
                //See if we are in the configs anywhere and have flags set
                if ((gridConfig != null) && regionInfos.RegionName != string.Empty)
                {
                    int newFlags = 0;
                    string regionName = regionInfos.RegionName.Trim().Replace(' ', '_');
                    newFlags = ParseFlags(newFlags, gridConfig.GetString("DefaultRegionFlags", String.Empty));
                    newFlags = ParseFlags(newFlags, gridConfig.GetString("Region_" + regionName, String.Empty));
                    newFlags = ParseFlags(newFlags,
                                          gridConfig.GetString("Region_" + regionInfos.RegionID.ToString(), String.Empty));
                    regionInfos.Flags = newFlags;
                }
            }

            //Set these so that we can make sure the region is online later
            regionInfos.Flags |= (int) RegionFlags.RegionOnline;
            regionInfos.Flags |= (int) RegionFlags.Safe;
            regionInfos.LastSeen = Util.UnixTimeSinceEpoch();

            if (region != null)
            {
                //If we already have a session, we need to check it
                if (!VerifyRegionSessionID(region, oldSessionID))
                {
                    MainConsole.Instance.WarnFormat(
                        "[GRID SERVICE]: Region {0} called register, but the sessionID they provided is wrong!",
                        region.RegionName);
                    return "Wrong Session ID";
                }
            }

            //Update the sessionID, use the old so that we don't generate a bunch of these
            SessionID = oldSessionID == UUID.Zero ? UUID.Random() : oldSessionID;
            regionInfos.SessionID = SessionID;

            // Everything is ok, let's register
            try
            {
                if (NeedToDeletePreviousRegion != UUID.Zero)
                    m_Database.Delete(NeedToDeletePreviousRegion);

                if (m_Database.Store(regionInfos))
                {
                    //Fire the event so that other modules notice
                    m_simulationBase.EventManager.FireGenericEventHandler("RegionRegistered", regionInfos);

                    //Get the neighbors for them
                    neighbors = GetNeighbors(regionInfos);
                    FixNeighbors(regionInfos, neighbors, false);

                    MainConsole.Instance.DebugFormat("[GRID SERVICE]: Region {0} registered successfully at {1}-{2}",
                                      regionInfos.RegionName, regionInfos.RegionLocX, regionInfos.RegionLocY);
                    return String.Empty;
                }
            }
            catch (Exception e)
            {
                MainConsole.Instance.WarnFormat("[GRID SERVICE]: Database exception: {0}", e);
            }

            return "Failed to save region into the database.";
        }

        public virtual bool VerifyRegionSessionID(GridRegion r, UUID SessionID)
        {
            if (m_UseSessionID && r.SessionID != SessionID)
                return false;
            return true;
        }

        public virtual string UpdateMap(GridRegion gregion, UUID sessionID)
        {
            GridRegion region = m_Database.Get(gregion.RegionID, gregion.ScopeID);
            if (region != null)
            {
                if (!VerifyRegionSessionID(region, sessionID))
                {
                    MainConsole.Instance.Warn(
                        "[GRID SERVICE]: Region called UpdateMap, but provided incorrect SessionID! Possible attempt to disable a region!!");
                    return "Wrong Session ID";
                }

                MainConsole.Instance.DebugFormat("[GRID SERVICE]: Region {0} updated its map", gregion.RegionID);

                m_Database.Delete(gregion.RegionID);

                region.Flags |= (int) RegionFlags.RegionOnline;

                region.TerrainImage = gregion.TerrainImage;
                region.TerrainMapImage = gregion.TerrainMapImage;
                region.SessionID = sessionID;
                //Update all of these as well, as they are able to be set by the region owner
                region.EstateOwner = gregion.EstateOwner;
                region.Access = gregion.Access;
                region.ExternalHostName = gregion.ExternalHostName;
                region.HttpPort = gregion.HttpPort;
                region.RegionName = gregion.RegionName;
                region.RegionType = gregion.RegionType;
                region.ServerURI = gregion.ServerURI;

                try
                {
                    region.LastSeen = Util.UnixTimeSinceEpoch();
                    m_Database.Store(region);
                }
                catch (Exception e)
                {
                    MainConsole.Instance.DebugFormat("[GRID SERVICE]: Database exception: {0}", e);
                }
            }

            return "";
        }

        public virtual bool DeregisterRegion(ulong regionHandle, UUID regionID, UUID SessionID)
        {
            GridRegion region = m_Database.Get(regionID, UUID.Zero);
            if (region == null)
                return false;

            if (!VerifyRegionSessionID(region, SessionID))
            {
                MainConsole.Instance.Warn(
                    "[GRID SERVICE]: Region called deregister, but provided incorrect SessionID! Possible attempt to disable a region!!");
                return false;
            }

            MainConsole.Instance.DebugFormat("[GRID SERVICE]: Region {0} deregistered", regionID);

            if (!m_DeleteOnUnregister || (region.Flags & (int) RegionFlags.Persistent) != 0 || !m_AllowNewRegistrations)
            {
                region.Flags &= ~(int) RegionFlags.RegionOnline;
                region.LastSeen = Util.UnixTimeSinceEpoch();
                try
                {
                    m_Database.Store(region);
                }
                catch (Exception e)
                {
                    MainConsole.Instance.DebugFormat("[GRID SERVICE]: Database exception: {0}", e);
                }

                FixNeighbors(region, GetNeighbors(region), true);
                return true;
            }

            FixNeighbors(region, GetNeighbors(region), true);

            return m_Database.Delete(regionID);
        }

        public virtual GridRegion GetRegionByUUID(UUID scopeID, UUID regionID)
        {
            return m_Database.Get(regionID, scopeID);
        }

        public virtual GridRegion GetRegionByPosition(UUID scopeID, int x, int y)
        {
            return m_Database.Get(x, y, scopeID);
        }

        public virtual GridRegion GetRegionByName(UUID scopeID, string regionName)
        {
            List<GridRegion> rdatas = m_Database.Get(regionName + "%", scopeID);
            if ((rdatas != null) && (rdatas.Count > 0))
            {
                //Sort to find the region with the exact name that was given
                rdatas.Sort(new RegionDataComparison(regionName));
                //Results are backwards... so it needs reversed
                rdatas.Reverse();
                return rdatas[0];
            }

            return null;
        }

        public virtual List<GridRegion> GetRegionsByName(UUID scopeID, string name, int maxNumber)
        {
            MainConsole.Instance.DebugFormat("[GRID SERVICE]: GetRegionsByName {0}", name);

            int count = 0;
            List<GridRegion> rinfos = new List<GridRegion>();
            List<GridRegion> rdatas = m_Database.Get(name + "%", scopeID);

            if (rdatas != null)
            {
                //Sort to find the region with the exact name that was given
                rdatas.Sort(new RegionDataComparison(name));
                //Results are backwards... so it needs reversed
                rdatas.Reverse();
#if (!ISWIN)
                foreach (GridRegion rdata in rdatas)
                {
                    if (count++ < maxNumber)
                    {
                        rinfos.Add(rdata);
                    }
                }
#else
                rinfos.AddRange(rdatas.Where(rdata => count++ < maxNumber));
#endif
            }

            return rinfos;
        }

        public virtual List<GridRegion> GetRegionRange(UUID scopeID, int xmin, int xmax, int ymin, int ymax)
        {
            return m_Database.Get(xmin, ymin, xmax, ymax, scopeID);
        }

        public class RegionDataComparison : IComparer<GridRegion>
        {
            private readonly string RegionName;

            public RegionDataComparison(string regionName)
            {
                RegionName = regionName;
            }

            #region IComparer<GridRegion> Members

            int IComparer<GridRegion>.Compare(GridRegion x, GridRegion y)
            {
                if (x.RegionName == RegionName)
                    return 1;
                else if (y.RegionName == RegionName)
                    return -1;
                else
                    return 0;
            }

            #endregion
        }

        #endregion

        #region Neighbor code

        private readonly Dictionary<UUID, List<GridRegion>> m_KnownNeighbors = new Dictionary<UUID, List<GridRegion>>();

        /// <summary>
        ///   Get the cached list of neighbors or a new list
        /// </summary>
        /// <param name = "region"></param>
        /// <returns></returns>
        public virtual List<GridRegion> GetNeighbors(GridRegion region)
        {
            List<GridRegion> neighbors = new List<GridRegion>();
            if (!m_KnownNeighbors.TryGetValue(region.RegionID, out neighbors))
            {
                neighbors = FindNewNeighbors(region);
                m_KnownNeighbors[region.RegionID] = neighbors;
            }
            GridRegion[] regions = new GridRegion[neighbors.Count];
            neighbors.CopyTo(regions);
            return new List<GridRegion>(regions);
        }

        private void FixNeighbors(GridRegion regionInfos, List<GridRegion> neighbors, bool down)
        {
            IAsyncMessagePostService postService = m_registryCore.RequestModuleInterface<IAsyncMessagePostService>();
            foreach (GridRegion r in neighbors)
            {
                if (m_KnownNeighbors.ContainsKey(r.RegionID))
                {
                    //Add/Remove them to/from the list
                    if (down)
                        m_KnownNeighbors[r.RegionID].Remove(regionInfos);
                    else if (m_KnownNeighbors[r.RegionID].Find(delegate(GridRegion rr)
                                                                   {
                                                                       if (rr.RegionID == regionInfos.RegionID)
                                                                           return true;
                                                                       return false;
                                                                   }) == null)
                        m_KnownNeighbors[r.RegionID].Add(regionInfos);
                }

                if (postService != null)
                    postService.Post(r.RegionHandle,
                                     SyncMessageHelper.NeighborChange(r.RegionID, regionInfos.RegionID, down));
            }

            if (down)
                m_KnownNeighbors.Remove(regionInfos.RegionID);
        }

        /// <summary>
        ///   Get all the neighboring regions of the given region
        /// </summary>
        /// <param name = "region"></param>
        /// <returns></returns>
        protected virtual List<GridRegion> FindNewNeighbors(GridRegion region)
        {
            int startX = (region.RegionLocX - 8192); //Give 8196 by default so that we pick up neighbors next to us
            int startY = (region.RegionLocY - 8192);
            if (MaxRegionSize != 0)
            {
                startX = (region.RegionLocX - MaxRegionSize);
                startY = (region.RegionLocY - MaxRegionSize);
            }

            //-1 so that we don't get size (256) + viewsize (256) and get a region two 256 blocks over
            int endX = (region.RegionLocX + RegionViewSize + region.RegionSizeX - 1);
            int endY = (region.RegionLocY + RegionViewSize + region.RegionSizeY - 1);

            List<GridRegion> neighbors = GetRegionRange(region.ScopeID, startX, endX, startY, endY);

            neighbors.RemoveAll(delegate(GridRegion r)
                                    {
                                        if (r.RegionID == region.RegionID)
                                            return true;

                                        if (r.RegionLocX + r.RegionSizeX - 1 < (region.RegionLocX - RegionViewSize) ||
                                            r.RegionLocY + r.RegionSizeY - 1 < (region.RegionLocY - RegionViewSize))
                                            //Check for regions outside of the boundry (created above when checking for large regions next to us)
                                            return true;

                                        return false;
                                    });
            return neighbors;
        }

        #endregion

        #region IService Members

        public virtual void Initialize(IConfigSource config, IRegistryCore registry)
        {
            IConfig handlerConfig = config.Configs["Handlers"];
            if (handlerConfig.GetString("GridHandler", "") != Name)
                return;

            //MainConsole.Instance.DebugFormat("[GRID SERVICE]: Starting...");
            Configure(config, registry);
        }

        #endregion

        private void HandleClearAllRegions(string[] cmd)
        {
            //Delete everything... give no criteria to just do 'delete from gridregions'
            m_Database.DeleteAll(new string[0], new object[0]);
            MainConsole.Instance.Warn("Cleared all regions");
        }

        private void HandleClearRegion(string[] cmd)
        {
            if (cmd.Length <= 3)
            {
                MainConsole.Instance.Warn("Wrong syntax, please check the help function and try again");
                return;
            }

            string regionName = Util.CombineParams(cmd, 3);
            GridRegion r = GetRegionByName(UUID.Zero, regionName);
            if (r == null)
            {
                MainConsole.Instance.Warn("Region was not found");
                return;
            }
            m_Database.Delete(r.RegionID);
        }

        private void HandleClearAllDownRegions(string[] cmd)
        {
            //Delete any flags with (Flags & 254) == 254
            m_Database.DeleteAll(new string[4] {"Flags", "Flags", "Flags", "Flags"},
                                 new object[4] {254, 267, 275, 296});
            MainConsole.Instance.Warn("Cleared all down regions");
        }

        private void HandleShowRegion(string[] cmd)
        {
            if (cmd.Length != 3)
            {
                MainConsole.Instance.Info("Syntax: show region <region name>");
                return;
            }
            List<GridRegion> regions = m_Database.Get(cmd[2], UUID.Zero);
            if (regions == null || regions.Count < 1)
            {
                MainConsole.Instance.Info("Region not found");
                return;
            }

            foreach (GridRegion r in regions)
            {
                MainConsole.Instance.Info("-------------------------------------------------------------------------------");
                RegionFlags flags = (RegionFlags) Convert.ToInt32(r.Flags);
                MainConsole.Instance.Info("Region Name: " + r.RegionName);
                MainConsole.Instance.Info("Region UUID: " + r.RegionID);
                MainConsole.Instance.Info("Region Location: " + String.Format("{0},{1}", r.RegionLocX, r.RegionLocY));
                MainConsole.Instance.Info("Region URI: " + r.ServerURI);
                MainConsole.Instance.Info("Region Owner: " + r.EstateOwner);
                MainConsole.Instance.Info("Region Flags: " + flags);
                MainConsole.Instance.Info("-------------------------------------------------------------------------------");
            }
            return;
        }

        private int ParseFlags(int prev, string flags)
        {
            RegionFlags f = (RegionFlags) prev;

            string[] parts = flags.Split(new[] {',', ' '}, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in parts)
            {
                int val;

                try
                {
                    if (p.StartsWith("+"))
                    {
                        val = (int) Enum.Parse(typeof (RegionFlags), p.Substring(1));
                        f |= (RegionFlags) val;
                    }
                    else if (p.StartsWith("-"))
                    {
                        val = (int) Enum.Parse(typeof (RegionFlags), p.Substring(1));
                        f &= ~(RegionFlags) val;
                    }
                    else
                    {
                        val = (int) Enum.Parse(typeof (RegionFlags), p);
                        f |= (RegionFlags) val;
                    }
                }
                catch (Exception)
                {
                    MainConsole.Instance.Info("Error in flag specification: " + p);
                }
            }

            return (int) f;
        }

        private void HandleSetFlags(string[] cmd)
        {
            if (cmd.Length < 5)
            {
                MainConsole.Instance.Info("Syntax: set region flags <region name> <flags>");
                return;
            }

            List<GridRegion> regions = m_Database.Get(cmd[3], UUID.Zero);
            if (regions == null || regions.Count < 1)
            {
                MainConsole.Instance.Info("Region not found");
                return;
            }

            foreach (GridRegion r in regions)
            {
                int flags = r.Flags;
                flags = ParseFlags(flags, cmd[4]);
                r.Flags = flags;
                RegionFlags f = (RegionFlags) flags;

                MainConsole.Instance.Info(String.Format("Set region {0} to {1}", r.RegionName, f));
                m_Database.Store(r);
            }
        }

        /// <summary>
        ///   Normalize the current float to the nearest block of 5 meters
        /// </summary>
        /// <param name = "number"></param>
        /// <returns></returns>
        private float NormalizePosition(float number)
        {
            try
            {
                if (float.IsNaN(number))
                    return 0;
                if (float.IsInfinity(number))
                    return 0;
                if (number < 0)
                    number = 0;
                double n = Math.Round(number, 0); //Remove the decimal
                string Number = n.ToString(); //Round the last

                string first = Number.Remove(Number.Length - 1);
                if (first == "")
                    return 0;
                int FirstNumber = 0;
                FirstNumber = first.StartsWith(".") ? 0 : int.Parse(first);

                string endNumber = Number.Remove(0, Number.Length - 1);
                if (endNumber == "")
                    return 0;
                float EndNumber = float.Parse(endNumber);
                if (EndNumber < 2.5f)
                    EndNumber = 0;
                else if (EndNumber > 7.5)
                {
                    EndNumber = 0;
                    FirstNumber++;
                }
                else
                    EndNumber = 5;
                return float.Parse(FirstNumber + EndNumber.ToString());
            }
            catch (Exception ex)
            {
                MainConsole.Instance.Error("[GridService]: Error in NormalizePosition " + ex);
            }
            return 0;
        }

        /// <summary>
        ///   Get all agent locations for the given region
        /// </summary>
        /// <param name = "X"></param>
        /// <param name = "Y"></param>
        /// <param name = "regionHandle"></param>
        /// <returns></returns>
        private List<mapItemReply> GetItems(int X, int Y, ulong regionHandle)
        {
            GridRegion region = GetRegionByPosition(UUID.Zero, X, Y);
            //if the region is down or doesn't exist, don't check it
            if (region == null || region.Access == (byte) SimAccess.Down ||
                region.Access == (byte) SimAccess.NonExistent)
                return new List<mapItemReply>();

            ICapsService capsService = m_registryCore.RequestModuleInterface<ICapsService>();
            if (capsService == null)
                return new List<mapItemReply>();

            IRegionCapsService regionCaps = capsService.GetCapsForRegion(regionHandle);
            if (regionCaps == null)
                return new List<mapItemReply>();

            Dictionary<Vector3, int> Positions = new Dictionary<Vector3, int>();
            //Get a list of all the clients in the region and add them
            foreach (IRegionClientCapsService clientCaps in regionCaps.GetClients())
            {
                //Only root agents!
                if (clientCaps.RootAgent)
                {
                    //Normalize the positions to 5 meter blocks so that agents stack instead of cover up each other
                    Vector3 position = new Vector3(NormalizePosition(clientCaps.LastPosition.X),
                                                   NormalizePosition(clientCaps.LastPosition.Y), 0);
                    int Number = 0;
                    //Find the number of agents currently at this position
                    if (!Positions.TryGetValue(position, out Number))
                        Number = 0;
                    Number++;
                    Positions[position] = Number;
                }
            }
            //Build the mapItemReply blocks
#if(!ISWIN)
            List<mapItemReply> mapItems = new List<mapItemReply>();
            foreach (KeyValuePair<Vector3, int> kvp in Positions)
            {
                mapItems.Add(new mapItemReply
                                                                           {
                                                                               x =
                                                                                   (uint)
                                                                                   (region.RegionLocX + kvp.Key.X),
                                                                               y =
                                                                                   (uint)
                                                                                   (region.RegionLocY + kvp.Key.Y),
                                                                               id = UUID.Zero,
                                                                               name =
                                                                                   Util.Md5Hash(region.RegionName +
                                                                                                Environment.TickCount.
                                                                                                    ToString()),
                                                                               Extra = kvp.Value,
                                                                               Extra2 = 0
                                                                           });
            }
#else
            List<mapItemReply> mapItems = Positions.Select(position => new mapItemReply
                                                                           {
                                                                               x =
                                                                                   (uint)
                                                                                   (region.RegionLocX + position.Key.X),
                                                                               y =
                                                                                   (uint)
                                                                                   (region.RegionLocY + position.Key.Y),
                                                                               id = UUID.Zero,
                                                                               name =
                                                                                   Util.Md5Hash(region.RegionName +
                                                                                                Environment.TickCount.
                                                                                                    ToString()),
                                                                               Extra = position.Value,
                                                                               Extra2 = 0
                                                                           }).ToList();
#endif

            //If there are no agents, we send one blank one to the client
            if (mapItems.Count == 0)
            {
                mapItemReply mapitem = new mapItemReply
                                           {
                                               x = (uint) (region.RegionLocX + 1),
                                               y = (uint) (region.RegionLocY + 1),
                                               id = UUID.Zero,
                                               name = Util.Md5Hash(region.RegionName + Environment.TickCount.ToString()),
                                               Extra = 0,
                                               Extra2 = 0
                                           };
                mapItems.Add(mapitem);
            }
            return mapItems;
        }
    }
}