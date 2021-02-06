using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using WebSocket4Net;
using Rhino.Geometry;
using System.Drawing;
using System.Net;
using System.IO;
using System.Threading.Tasks;

namespace gosand
{
    /// <summary>
    /// Get Mesh Component for Gosand
    /// </summary>
    public class GosandGetMesh : GH_Component
    {
        public GosandGetMesh() : base("Get Gosand Mesh", "GetMesh", "Loads a Mesh from a Gosand source", "Gosand", "Device") { }
        
        /// <summary>
        /// Register Input Ports
        /// </summary>
        /// <param name="pManager"></param>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Url", "U", "Url to Gosand webserver. The url should only contain protocol, hostname and port like: http://localhost:4777 - also supports websocket connections for example: ws://localhost:4777", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Freq", "f", "[Optional, Default 300] Receiving frequency in ms. This forces the canvas to resolve.", GH_ParamAccess.item);
            pManager.AddColourParameter("Color Palette", "C", "[Optional, Default Black] Depth Color Palette must contain 255 Colors to cover the full depth palette.", GH_ParamAccess.list);
            pManager.AddNumberParameter("X Scale", "x", "[Optional, Default 1.0] Scale for X dimension", GH_ParamAccess.item);
            pManager.AddNumberParameter("Y Scale", "y", "[Optional, Default 1.0] Scale for Y dimension", GH_ParamAccess.item);
            pManager.AddNumberParameter("Z Scale", "d", "[Optional, Default 1.0] Scale for Z dimension", GH_ParamAccess.item);
            pManager.AddRectangleParameter("Cropping Frame", "cf", "[Optional, Default None] Cropping Rectangle.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Contour Distance", "cd", "[Optional, Default 0.0] Contour curve distance for spacing", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Circle Detection", "c", "[Optional, Default true] Circle Detection",GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
            pManager[8].Optional = true;
        }

        /// <summary>
        /// Register Output Ports
        /// </summary>
        /// <param name="pManager"></param>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Mesh", GH_ParamAccess.item);
            pManager.AddCurveParameter("Contour Curves", "C", "Contour Curves", GH_ParamAccess.list);
            pManager.AddCircleParameter("Circles", "C", "Detected Circles", GH_ParamAccess.list);
        }

        /// <summary>
        /// Get Bytes from Gosand Server
        /// </summary>
        /// <param name="uri">Gosand server url</param>
        /// <returns>Response in bytes</returns>
        public void GetBytes(string uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    this.buffer = reader.ReadToEnd();
                }
                else
                {
                    ShowComponentError("Could not connect to Server");
                }
            }
        }

        /// <summary>
        /// keep Websocket alive after refresh
        /// </summary>
        private WebSocket websocket;
        
        /// <summary>
        /// Default refresh rate 300ms
        /// </summary>
        private int tickRate = 300;

        /// <summary>
        /// The actual output when processing data from Kinect
        /// </summary>
        GosandData result = new GosandData();

        /// <summary>
        /// indicates if a background task is still in process
        /// </summary>
        bool taskInProcess;

        /// <summary>
        /// The previous processing result
        /// </summary>
        GosandData previousResult = new GosandData();

        /// <summary>
        /// Gosand data buffer for websocket data
        /// </summary>
        private string buffer;

        /// <summary>
        /// Scale
        /// </summary>
        private Point3d scale;

        /// <summary>
        /// Triggered when solving the instance
        /// </summary>
        /// <param name="DA"></param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!result.HasData()) result = previousResult;
            DA.SetData(0, result.Mesh);
            DA.SetDataList(1, result.Curves);
            DA.SetDataList(2, result.Circles);

            if (!taskInProcess)
            {
                //
                // Get all input params
                //
                GH_String path = new GH_String();
                DA.GetData<GH_String>(0, ref path);
                GH_Integer frequency = new GH_Integer(300);
                if (DA.GetData<GH_Integer>(1, ref frequency))
                {
                    if (frequency.Value < 50 && frequency.Value > 0) { frequency.Value = 50; }
                    tickRate = frequency.Value;
                }
                List<GH_Colour> palette = new List<GH_Colour>();
                DA.GetDataList<GH_Colour>(2, palette);
                if (palette.Count < 255) palette = null;
                GH_Number xscale = new GH_Number(1.0);
                DA.GetData<GH_Number>(3, ref xscale);
                GH_Number yscale = new GH_Number(1.0);
                DA.GetData<GH_Number>(4, ref yscale);
                GH_Number dscale = new GH_Number(1.0);
                DA.GetData<GH_Number>(5, ref dscale);
                this.scale = new Point3d(xscale.Value, yscale.Value, dscale.Value);
                GH_Rectangle crect = new GH_Rectangle();
                if (!DA.GetData<GH_Rectangle>(6, ref crect)) { crect = null; }
                GH_Number distance = new GH_Number(0.0);
                DA.GetData<GH_Number>(7, ref distance);
                GH_Boolean circ = new GH_Boolean(false);
                DA.GetData<GH_Boolean>(8, ref circ);

                //
                // Process retrieving data and meshing async
                //
                Task<GosandData> computingTask = new Task<GosandData>(() => generateMesh(path.Value, palette, crect, frequency.Value, distance.Value, circ.Value));
                computingTask.ContinueWith(r =>
                {
                    if (r.Status == TaskStatus.RanToCompletion)
                    {
                        var task_result = computingTask.Result;
                        result = task_result;
                        if (task_result.HasData())
                        {
                            previousResult = task_result;
                        }
                        taskInProcess = false;
                    }
                    else if (r.Status == TaskStatus.Faulted)
                    {
                        result = new GosandData();
                        taskInProcess = false;
                        ShowComponentError("Could not connect to Server");
                    }
                },
                TaskScheduler.FromCurrentSynchronizationContext());
                computingTask.Start();
                taskInProcess = true;
            }

            ScheduleSolve();
        }

        private GosandData generateMesh(string url, List<GH_Colour> palette, GH_Rectangle crect, int frequency, double distance, bool circ)
        {
            //
            // Connect to Gosand sever either using a websocket or by simple GET requests
            //
            string path = circ ? "?detection=true" : "";

            if (!url.ToLower().StartsWith("ws"))
            {
                GetBytes(String.Format("{0}/data/{1}", url, path));
                if (this.buffer != null)
                {
                    return GosandData.FromResponseString(this.buffer, palette, this.scale, crect, distance);
                }
            }
            else
            {
                this.buffer = null;
                if (websocket == null || websocket.State == WebSocketState.Closed)
                {
                    int server_refresh_rate = frequency < 50 ? 50 : frequency;
                    
                    websocket = new WebSocket(String.Format("{0}/stream/{1}/{2}", url, server_refresh_rate, path));
                    websocket.EnableAutoSendPing = true;
                    websocket.MessageReceived += Websocket_MessageReceived;
                    websocket.Open();
                    while (websocket.State == WebSocketState.Connecting) { }
                }
                while (websocket.State == WebSocketState.Open)
                {
                    if (this.buffer != null)
                    {
                        return GosandData.FromResponseString(this.buffer,palette, this.scale, crect, distance);
                    }
                }
            }
            return new GosandData();
        }

        /// <summary>
        /// Triggere when receiving websocket messages
        /// Using text/messages on purpose because binary payloads failed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Websocket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            buffer = e.Message;              
        }

        /// <summary>
        /// Show component error message
        /// </summary>
        /// <param name="errorMessage"></param>
        protected void ShowComponentError(string errorMessage)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, errorMessage);
            ScheduleSolve();
        }

        protected void ScheduleDelegate(GH_Document doc)
        {
            ExpireSolution(false);
        }

        protected void ScheduleSolve()
        {
            if (tickRate > 0)
                OnPingDocument().ScheduleSolution(tickRate, ScheduleDelegate);
        }

        public override Guid ComponentGuid
        {
            get
            {
                return new Guid("{b8da856a-357e-4c7b-a312-9724061aa59a}");
            }
        }

        protected override Bitmap Icon => gosand.Properties.Resources.kinect;
    }


}
