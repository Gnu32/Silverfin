/*
 * Copyright (c) Contributors, http://aurora-sim.org/
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

using System.Collections.Generic;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Aurora.Framework;
using Aurora.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Services.CapsService
{
    public class MapCAPS : ICapsServiceConnector
    {
        private const int m_mapDistance = 100;
        private readonly List<MapBlockData> m_mapLayer = new List<MapBlockData>();
        private readonly string m_mapLayerPath = "0001";
        private bool m_allowCapsMessage = true;
        private IGridService m_gridService;
        private IRegionClientCapsService m_service;

        #region ICapsServiceConnector Members

        public void RegisterCaps(IRegionClientCapsService service)
        {
            m_service = service;
            m_gridService = service.Registry.RequestModuleInterface<IGridService>();
            IConfig config =
                service.ClientCaps.Registry.RequestModuleInterface<ISimulationBase>().ConfigSource.Configs["MapCaps"];
            if (config != null)
                m_allowCapsMessage = config.GetBoolean("AllowCapsMessage", m_allowCapsMessage);

#if (!ISWIN)
            RestMethod method = delegate(string request, string path, string param,
                                                                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                return MapLayerRequest(request, path, param, httpRequest, httpResponse, m_service.AgentID);
            };
#else
            RestMethod method =
                (request, path, param, httpRequest, httpResponse) =>
                MapLayerRequest(request, path, param, httpRequest, httpResponse,
                                m_service.AgentID);
#endif
            m_service.AddStreamHandler("MapLayer",
                                       new RestStreamHandler("POST", m_service.CreateCAPS("MapLayer", m_mapLayerPath),
                                                             method));
            m_service.AddStreamHandler("MapLayerGod",
                                       new RestStreamHandler("POST", m_service.CreateCAPS("MapLayerGod", m_mapLayerPath),
                                                             method));
        }

        public void EnteringRegion()
        {
            m_mapLayer.Clear();
        }

        public void DeregisterCaps()
        {
            m_service.RemoveStreamHandler("MapLayer", "POST");
            m_mapLayer.Clear();
        }

        #endregion

        /// <summary>
        ///   Callback for a map layer request
        /// </summary>
        /// <param name = "request"></param>
        /// <param name = "path"></param>
        /// <param name = "param"></param>
        /// <param name = "agentID"></param>
        /// <param name = "caps"></param>
        /// <returns></returns>
        public string MapLayerRequest(string request, string path, string param,
                                      OSHttpRequest httpRequest, OSHttpResponse httpResponse, UUID agentID)
        {
            int bottom = (m_service.RegionY/Constants.RegionSize) - m_mapDistance;
            int top = (m_service.RegionY/Constants.RegionSize) + m_mapDistance;
            int left = (m_service.RegionX/Constants.RegionSize) - m_mapDistance;
            int right = (m_service.RegionX/Constants.RegionSize) + m_mapDistance;

            OSDMap map = (OSDMap) OSDParser.DeserializeLLSDXml(request);

            int flags = map["Flags"].AsInteger();

            OSDArray layerData = new OSDArray
                                     {
                                         GetOSDMapLayerResponse(bottom, left, right, top,
                                                                new UUID("00000000-0000-1111-9999-000000000006"))
                                     };
            OSDArray mapBlocksData = new OSDArray();

            if (m_allowCapsMessage)
            {
                if (m_mapLayer == null || m_mapLayer.Count == 0)
                {
                    List<GridRegion> regions = m_gridService.GetRegionRange(UUID.Zero,
                                                                            left*Constants.RegionSize,
                                                                            right*Constants.RegionSize,
                                                                            bottom*Constants.RegionSize,
                                                                            top*Constants.RegionSize);
                    foreach (GridRegion r in regions)
                    {
                        m_mapLayer.Add(flags == 0 ? MapBlockFromGridRegion(r) : TerrainBlockFromGridRegion(r));
                    }
                }
            }
            foreach (MapBlockData block in m_mapLayer)
            {
                //Add to the array
                mapBlocksData.Add(block.ToOSD());
            }
            OSDMap response = MapLayerResponce(layerData, mapBlocksData, flags);
            string resp = OSDParser.SerializeLLSDXmlString(response);
            return resp;
        }

        protected MapBlockData MapBlockFromGridRegion(GridRegion r)
        {
            MapBlockData block = new MapBlockData();
            if (r == null)
            {
                block.Access = (byte) SimAccess.Down;
                block.MapImageID = UUID.Zero;
                return block;
            }
            block.Access = r.Access;
            block.MapImageID = r.TerrainImage;
            block.Name = r.RegionName;
            block.X = (ushort) (r.RegionLocX/Constants.RegionSize);
            block.Y = (ushort) (r.RegionLocY/Constants.RegionSize);
            block.SizeX = (ushort) (r.RegionSizeX);
            block.SizeY = (ushort) (r.RegionSizeY);
            return block;
        }

        protected MapBlockData TerrainBlockFromGridRegion(GridRegion r)
        {
            MapBlockData block = new MapBlockData();
            if (r == null)
            {
                block.Access = (byte) SimAccess.Down;
                block.MapImageID = UUID.Zero;
                return block;
            }
            block.Access = r.Access;
            block.MapImageID = r.TerrainMapImage;
            block.Name = r.RegionName;
            block.X = (ushort) (r.RegionLocX/Constants.RegionSize);
            block.Y = (ushort) (r.RegionLocY/Constants.RegionSize);
            return block;
        }

        protected static OSDMap MapLayerResponce(OSDArray layerData, OSDArray mapBlocksData, int flags)
        {
            OSDMap map = new OSDMap();
            OSDMap agentMap = new OSDMap();
            agentMap["Flags"] = flags;
            map["AgentData"] = agentMap;
            map["LayerData"] = layerData;
            map["MapBlocks"] = mapBlocksData;
            return map;
        }

        ///<summary>
        ///</summary>
        ///<returns></returns>
        protected static OSDMap GetOSDMapLayerResponse(int bottom, int left, int right, int top, UUID imageID)
        {
            OSDMap mapLayer = new OSDMap();
            mapLayer["Bottom"] = bottom;
            mapLayer["Left"] = left;
            mapLayer["Right"] = right;
            mapLayer["Top"] = top;
            mapLayer["ImageID"] = imageID;

            return mapLayer;
        }
    }
}