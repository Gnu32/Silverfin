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

using System;
using System.Collections;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using Aurora.Simulation.Base;
using OpenSim.Services.Interfaces;
using Aurora.Framework;
using Aurora.Framework.Servers.HttpServer;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Imaging;

namespace OpenSim.Services.CapsService
{
    public class AssetCAPS : ICapsServiceConnector
    {
        #region Stream Handler

        public delegate byte[] StreamHandlerCallback(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse);

        public class StreamHandler : BaseStreamHandler
        {
            readonly StreamHandlerCallback m_callback;

            public StreamHandler(string httpMethod, string path, StreamHandlerCallback callback)
                : base(httpMethod, path)
            {
                m_callback = callback;
            }

            public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                return m_callback(path, request, httpRequest, httpResponse);
            }
        }

        #endregion Stream Handler

        private const string m_uploadBakedTexturePath = "0010";
        protected IAssetService m_assetService;
        protected IRegionClientCapsService m_service;
        public const string DefaultFormat = "x-j2c";
        // TODO: Change this to a config option
        protected string REDIRECT_URL = null;

        public void RegisterCaps(IRegionClientCapsService service)
        {
            m_service = service;
            m_assetService = service.Registry.RequestModuleInterface<IAssetService>();

            service.AddStreamHandler("GetTexture",
                new StreamHandler("GET", service.CreateCAPS("GetTexture", ""),
                                                        ProcessGetTexture));
            service.AddStreamHandler("UploadBakedTexture",
                new RestStreamHandler("POST", service.CreateCAPS("UploadBakedTexture", m_uploadBakedTexturePath),
                                                        UploadBakedTexture));
            service.AddStreamHandler("GetMesh",
                new RestHTTPHandler("GET", service.CreateCAPS("GetMesh", ""),
                                                       ProcessGetMesh));
        }

        public void EnteringRegion()
        {
        }

        public void DeregisterCaps()
        {
            m_service.RemoveStreamHandler("GetTexture", "GET");
            m_service.RemoveStreamHandler("UploadBakedTexture", "POST");
            m_service.RemoveStreamHandler("GetMesh", "GET");
        }

        #region Get Texture

        private byte[] ProcessGetTexture(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            //MainConsole.Instance.DebugFormat("[GETTEXTURE]: called in {0}", m_scene.RegionInfo.RegionName);

            // Try to parse the texture ID from the request URL
            NameValueCollection query = HttpUtility.ParseQueryString(httpRequest.Url.Query);
            string textureStr = query.GetOne("texture_id");
            string format = query.GetOne("format");

            if (m_assetService == null)
            {
                httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                return null;
            }

            UUID textureID;
            if (!String.IsNullOrEmpty(textureStr) && UUID.TryParse(textureStr, out textureID))
            {
                string[] formats;
                if (!string.IsNullOrEmpty(format))
                {
                    formats = new[] { format.ToLower() };
                }
                else
                {
                    formats = WebUtils.GetPreferredImageTypes(httpRequest.Headers.Get("Accept"));
                    if (formats.Length == 0)
                        formats = new[] { DefaultFormat }; // default
                }
                // OK, we have an array with preferred formats, possibly with only one entry
                httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                foreach (string f in formats)
                {
                    if (FetchTexture(httpRequest, httpResponse, textureID, f))
                        break;
                }
            }
            else
            {
                MainConsole.Instance.Warn("[GETTEXTURE]: Failed to parse a texture_id from GetTexture request: " + httpRequest.Url);
            }

            httpResponse.Send();
            httpRequest.InputStream.Close();
            httpRequest = null;
            return null;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="httpRequest"></param>
        /// <param name="httpResponse"></param>
        /// <param name="textureID"></param>
        /// <param name="format"></param>
        /// <returns>False for "caller try another codec"; true otherwise</returns>
        private bool FetchTexture(OSHttpRequest httpRequest, OSHttpResponse httpResponse, UUID textureID, string format)
        {
            MainConsole.Instance.DebugFormat("[GETTEXTURE]: {0} with requested format {1}", textureID, format);
            AssetBase texture;

            string fullID = textureID.ToString();
            if (format != DefaultFormat)
                fullID = fullID + "-" + format;

            if (!String.IsNullOrEmpty(REDIRECT_URL))
            {
                // Only try to fetch locally cached textures. Misses are redirected
                texture = m_assetService.GetCached(fullID);

                if (texture != null)
                {
                    if (texture.Type != (sbyte)AssetType.Texture)
                    {
                        httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                        return true;
                    }
                    WriteTextureData(httpRequest, httpResponse, texture, format);
                }
                else
                {
                    string textureUrl = REDIRECT_URL + textureID.ToString();
                    MainConsole.Instance.Debug("[GETTEXTURE]: Redirecting texture request to " + textureUrl);
                    httpResponse.RedirectLocation = textureUrl;
                    return true;
                }
            }
            else // no redirect
            {
                // try the cache
                texture = m_assetService.GetCached(fullID);

                if (texture == null)
                {
                    //MainConsole.Instance.DebugFormat("[GETTEXTURE]: texture was not in the cache");

                    // Fetch locally or remotely. Misses return a 404
                    texture = m_assetService.Get(textureID.ToString());

                    if (texture != null)
                    {
                        if (texture.Type != (sbyte)AssetType.Texture)
                        {
                            httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                            return true;
                        }
                        if (format == DefaultFormat)
                        {
                            WriteTextureData(httpRequest, httpResponse, texture, format);
                            texture = null;
                            return true;
                        }
                        AssetBase newTexture = new AssetBase(texture.ID + "-" + format, texture.Name, AssetType.Texture,
                                                             texture.CreatorID)
                                                   {Data = ConvertTextureData(texture, format)};
                        if (newTexture.Data.Length == 0)
                            return false; // !!! Caller try another codec, please!

                        newTexture.Flags = AssetFlags.Collectable | AssetFlags.Temporary;
                        newTexture.ID = m_assetService.Store(newTexture);
                        WriteTextureData(httpRequest, httpResponse, newTexture, format);
                        newTexture = null;
                        return true;
                    }
                }
                else // it was on the cache
                {
                    if (texture.Type != (sbyte)AssetType.Texture)
                    {
                        httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                        return true;
                    }
                    //MainConsole.Instance.DebugFormat("[GETTEXTURE]: texture was in the cache");
                    WriteTextureData(httpRequest, httpResponse, texture, format);
                    texture = null;
                    return true;
                }

            }

            // not found
            MainConsole.Instance.Warn("[GETTEXTURE]: Texture " + textureID + " not found");
            httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
            return true;
        }

        private void WriteTextureData(OSHttpRequest request, OSHttpResponse response, AssetBase texture, string format)
        {
            m_service.Registry.RequestModuleInterface<ISimulationBase>().EventManager.FireGenericEventHandler(
                "AssetRequested", new object[] {m_service.Registry, texture, m_service.AgentID});

            string range = request.Headers.GetOne("Range");
            //MainConsole.Instance.DebugFormat("[GETTEXTURE]: Range {0}", range);
            if (!String.IsNullOrEmpty(range)) // JP2's only
            {
                // Range request
                int start, end;
                if (TryParseRange(range, out start, out end))
                {
                    // Before clamping start make sure we can satisfy it in order to avoid
                    // sending back the last byte instead of an error status
                    if (start >= texture.Data.Length)
                    {
                        response.StatusCode = (int)System.Net.HttpStatusCode.RequestedRangeNotSatisfiable;
                    }
                    else
                    {
                        end = Utils.Clamp(end, 0, texture.Data.Length - 1);
                        start = Utils.Clamp(start, 0, end);
                        int len = end - start + 1;

                        //MainConsole.Instance.Debug("Serving " + start + " to " + end + " of " + texture.Data.Length + " bytes for texture " + texture.ID);

                        if (len < texture.Data.Length)
                            response.StatusCode = (int)System.Net.HttpStatusCode.PartialContent;
                        else
                            response.StatusCode = (int)System.Net.HttpStatusCode.OK;

                        response.ContentLength = len;
                        response.ContentType = texture.TypeString;
                        response.AddHeader("Content-Range", String.Format("bytes {0}-{1}/{2}", start, end, texture.Data.Length));

                        response.Body.Write(texture.Data, start, len);
                    }
                }
                else
                {
                    MainConsole.Instance.Warn("[GETTEXTURE]: Malformed Range header: " + range);
                    response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
                }
            }
            else // JP2's or other formats
            {
                // Full content request
                response.StatusCode = (int)System.Net.HttpStatusCode.OK;
                response.ContentLength = texture.Data.Length;
                response.ContentType = texture.TypeString;
                if (format == DefaultFormat)
                    response.ContentType = texture.TypeString;
                else
                    response.ContentType = "image/" + format;
                response.Body.Write(texture.Data, 0, texture.Data.Length);
            }
        }

        private bool TryParseRange(string header, out int start, out int end)
        {
            if (header.StartsWith("bytes="))
            {
                string[] rangeValues = header.Substring(6).Split('-');
                if (rangeValues.Length == 2)
                {
                    if (Int32.TryParse(rangeValues[0], out start) && Int32.TryParse(rangeValues[1], out end))
                        return true;
                }
            }

            start = end = 0;
            return false;
        }

        private byte[] ConvertTextureData(AssetBase texture, string format)
        {
            MainConsole.Instance.DebugFormat("[GETTEXTURE]: Converting texture {0} to {1}", texture.ID, format);
            byte[] data = new byte[0];

            MemoryStream imgstream = new MemoryStream();
            Bitmap mTexture = new Bitmap(1, 1);
            ManagedImage managedImage;
            Image image = mTexture;

            try
            {
                // Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular data

                imgstream = new MemoryStream();

                // Decode image to System.Drawing.Image
                if (OpenJPEG.DecodeToImage(texture.Data, out managedImage, out image))
                {
                    // Save to bitmap
                    mTexture = new Bitmap(image);

                    EncoderParameters myEncoderParameters = new EncoderParameters();
                    myEncoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 95L);

                    // Save bitmap to stream
                    ImageCodecInfo codec = GetEncoderInfo("image/" + format);
                    if (codec != null)
                    {
                        mTexture.Save(imgstream, codec, myEncoderParameters);
                        // Write the stream to a byte array for output
                        data = imgstream.ToArray();
                    }
                    else
                        MainConsole.Instance.WarnFormat("[GETTEXTURE]: No such codec {0}", format);

                }
            }
            catch (Exception e)
            {
                MainConsole.Instance.WarnFormat("[GETTEXTURE]: Unable to convert texture {0} to {1}: {2}", texture.ID, format, e.Message);
            }
            finally
            {
                // Reclaim memory, these are unmanaged resources
                // If we encountered an exception, one or more of these will be null
                mTexture.Dispose();
                mTexture = null;
                managedImage = null;

                if (image != null)
                    image.Dispose();
                image = null;

                imgstream.Close();
                imgstream = null;
            }

            return data;
        }

        // From msdn
        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();
#if (!ISWIN)
            foreach (ImageCodecInfo t in encoders)
            {
                if (t.MimeType == mimeType) return t;
            }
            return null;
#else
            return encoders.FirstOrDefault(t => t.MimeType == mimeType);
#endif
        }

        #endregion

        #region Baked Textures

        public string UploadBakedTexture(string request, string path,
                string param, OSHttpRequest httpRequest,
                OSHttpResponse httpResponse)
        {
            try
            {
                //MainConsole.Instance.Debug("[CAPS]: UploadBakedTexture Request in region: " +
                //        m_regionName);

                string uploaderPath = UUID.Random().ToString();
                string uploadpath = m_service.CreateCAPS("Upload" + uploaderPath, uploaderPath);
                BakedTextureUploader uploader =
                    new BakedTextureUploader(uploadpath, "Upload" + uploaderPath,
                        m_service);
                uploader.OnUpLoad += BakedTextureUploaded;

                m_service.AddStreamHandler(uploadpath,
                        new BinaryStreamHandler("POST", uploadpath,
                        uploader.uploaderCaps));

                string uploaderURL = m_service.HostUri + uploadpath;
                OSDMap map = new OSDMap();
                map["uploader"] = uploaderURL;
                map["state"] = "upload";
                return OSDParser.SerializeLLSDXmlString(map);
            }
            catch (Exception e)
            {
                MainConsole.Instance.Error("[CAPS]: " + e);
            }

            return null;
        }

        public delegate void UploadedBakedTexture(byte[] data, out UUID newAssetID);
        public class BakedTextureUploader
        {
            public event UploadedBakedTexture OnUpLoad;
            private UploadedBakedTexture handlerUpLoad = null;

            private readonly string uploaderPath = String.Empty;
            private readonly string uploadMethod = "";
            private readonly IRegionClientCapsService clientCaps;

            public BakedTextureUploader(string path, string method, IRegionClientCapsService caps)
            {
                uploaderPath = path;
                uploadMethod = method;
                clientCaps = caps;
            }

            /// <summary>
            ///
            /// </summary>
            /// <param name="data"></param>
            /// <param name="path"></param>
            /// <param name="param"></param>
            /// <returns></returns>
            public string uploaderCaps(byte[] data, string path, string param)
            {
                handlerUpLoad = OnUpLoad;
                UUID newAssetID;
                handlerUpLoad(data, out newAssetID);

                string res = String.Empty;
                OSDMap map = new OSDMap();
                map["new_asset"] = newAssetID.ToString();
                map["item_id"] = UUID.Zero;
                map["state"] = "complete";
                res = OSDParser.SerializeLLSDXmlString(map);
                clientCaps.RemoveStreamHandler(uploadMethod, "POST", uploaderPath);

                return res;
            }
        }

        public void BakedTextureUploaded(byte[] data, out UUID newAssetID)
        {
            //MainConsole.Instance.InfoFormat("[AssetCAPS]: Received baked texture {0}", assetID.ToString());
            AssetBase asset = new AssetBase(UUID.Random(), "Baked Texture", AssetType.Texture, m_service.AgentID)
                                  {Data = data, Flags = AssetFlags.Deletable | AssetFlags.Temporary};
            newAssetID = m_assetService.Store(asset);
            MainConsole.Instance.DebugFormat("[AssetCAPS]: Baked texture new id {0}", asset.ID.ToString());
            asset.ID = newAssetID;
        }

        public Hashtable ProcessGetMesh(Hashtable request)
        {
            Hashtable responsedata = new Hashtable();
            responsedata["int_response_code"] = 400; //501; //410; //404;
            responsedata["content_type"] = "text/plain";
            responsedata["keepalive"] = false;
            responsedata["str_response_string"] = "Request wasn't what was expected";

            string meshStr = string.Empty;

            if (request.ContainsKey("mesh_id"))
                meshStr = request["mesh_id"].ToString();


            UUID meshID = UUID.Zero;
            if (!String.IsNullOrEmpty(meshStr) && UUID.TryParse(meshStr, out meshID))
            {
                if (m_assetService == null)
                {
                    responsedata["int_response_code"] = 404; //501; //410; //404;
                    responsedata["content_type"] = "text/plain";
                    responsedata["keepalive"] = false;
                    responsedata["str_response_string"] = "The asset service is unavailable.  So is your mesh.";
                    return responsedata;
                }

                // Only try to fetch locally cached textures. Misses are redirected
                AssetBase mesh = m_assetService.GetCached(meshID.ToString());
                if (mesh != null)
                {
                    if (mesh.Type == (SByte)AssetType.Mesh)
                    {
                        responsedata["str_response_string"] = Convert.ToBase64String(mesh.Data);
                        responsedata["content_type"] = "application/vnd.ll.mesh";
                        responsedata["int_response_code"] = 200;
                    }
                    // Optionally add additional mesh types here
                    else
                    {
                        responsedata["int_response_code"] = 404; //501; //410; //404;
                        responsedata["content_type"] = "text/plain";
                        responsedata["keepalive"] = false;
                        responsedata["str_response_string"] = "Unfortunately, this asset isn't a mesh.";
                        return responsedata;
                    }
                }
                else
                {
                    mesh = m_assetService.Get(meshID.ToString());
                    if (mesh != null)
                    {
                        if (mesh.Type == (SByte)AssetType.Mesh)
                        {
                            responsedata["str_response_string"] = Convert.ToBase64String(mesh.Data);
                            responsedata["content_type"] = "application/vnd.ll.mesh";
                            responsedata["int_response_code"] = 200;
                        }
                        // Optionally add additional mesh types here
                        else
                        {
                            responsedata["int_response_code"] = 404; //501; //410; //404;
                            responsedata["content_type"] = "text/plain";
                            responsedata["keepalive"] = false;
                            responsedata["str_response_string"] = "Unfortunately, this asset isn't a mesh.";
                            return responsedata;
                        }
                    }

                    else
                    {
                        responsedata["int_response_code"] = 404; //501; //410; //404;
                        responsedata["content_type"] = "text/plain";
                        responsedata["keepalive"] = false;
                        responsedata["str_response_string"] = "Your Mesh wasn't found.  Sorry!";
                        return responsedata;
                    }
                }

            }

            return responsedata;
        }

        #endregion
    }
}
