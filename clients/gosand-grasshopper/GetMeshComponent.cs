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
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
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
        public Tuple<byte[],List<GH_Circle>> GetBytes(string uri)
        {
            byte[] bytes;
            List<GH_Circle> cs;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(reader.ReadToEnd());
                    bytes = Convert.FromBase64String((string)payload["d"]);
                    cs = new List<GH_Circle>();
                    if (payload["c"] != null)
                    {
                        foreach (IDictionary<string, Newtonsoft.Json.Linq.JToken> dict in (ICollection<Newtonsoft.Json.Linq.JToken>)payload["c"])
                        {
                            var pt = new Point3d(dict["x"].ToObject<int>(), dict["y"].ToObject<int>(),dict["z"].ToObject<int>());
                            cs.Add(new GH_Circle(new Circle(pt, dict["r"].ToObject<int>())));
                        }
                    }
                }
                else
                {
                    ShowComponentError("Could not connect to Server");
                    return null;
                }
            }
            return new Tuple<byte[], List<GH_Circle>>(bytes, cs);
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
        Tuple<GH_Mesh, Curve[], List<GH_Circle>> result = new Tuple<GH_Mesh, Curve[], List<GH_Circle>>(null,null,null);

        /// <summary>
        /// indicates if a background task is still in process
        /// </summary>
        bool taskInProcess;

        /// <summary>
        /// The previous processing result
        /// </summary>
        Tuple<GH_Mesh, Curve[], List<GH_Circle>> previousResult = new Tuple<GH_Mesh, Curve[], List<GH_Circle>>(null,null, null);

        /// <summary>
        /// Byte Array buffer for websocket data
        /// </summary>
        private byte[] buffer;

        /// <summary>
        /// Circle buffer for websocket data
        /// </summary>
        private List<GH_Circle> circles;

        /// <summary>
        /// Triggered when solving the instance
        /// </summary>
        /// <param name="DA"></param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (result.Item1 == null) result = previousResult;
            DA.SetData(0, result.Item1);
            DA.SetDataList(1, result.Item2);
            DA.SetDataList(2, result.Item3);

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
                GH_Rectangle crect = new GH_Rectangle();
                if (!DA.GetData<GH_Rectangle>(6, ref crect)) { crect = null; }
                GH_Number distance = new GH_Number(0.0);
                DA.GetData<GH_Number>(7, ref distance);

                //
                // Process retrieving data and meshing async
                //
                Task<Tuple<GH_Mesh, Curve[], List<GH_Circle>>> computingTask = new Task<Tuple<GH_Mesh, Curve[], List<GH_Circle>>>(() => generateMesh(path.Value, palette, xscale.Value, yscale.Value, dscale.Value, crect, frequency.Value, distance.Value));
                computingTask.ContinueWith(r =>
                {
                    if (r.Status == TaskStatus.RanToCompletion)
                    {
                        var task_result = computingTask.Result;
                        result = task_result;
                        if (task_result.Item1 != null)
                        {
                            previousResult = task_result;
                        }
                        taskInProcess = false;
                    }
                    else if (r.Status == TaskStatus.Faulted)
                    {
                        result = new Tuple<GH_Mesh, Curve[], List<GH_Circle>>(null,null,null);
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

        private Tuple<GH_Mesh, Curve[], List<GH_Circle>> generateMesh(string path, List<GH_Colour> palette, double xscale, double yscale, double dscale, GH_Rectangle crect, int frequency, double distance)
        {
            //
            // Connect to Gosand sever either using a websocket or by simple GET requests
            //
            if (!path.ToLower().StartsWith("ws"))
            {
                var data = GetBytes(String.Format("{0}/data/", path));
                if (data != null)
                {
                    Color[] colors = null;
                    var pointaray = depthArrayToPointArray(data.Item1, palette, out colors, xscale, yscale, dscale);
                    var m = createMesh(pointaray, colors, crect, dscale, distance);
                    return new Tuple<GH_Mesh, Curve[], List<GH_Circle>>(m.Item1, m.Item2, data.Item2);
                }
            }
            else
            {
                buffer = null;
                if (websocket == null || websocket.State == WebSocketState.Closed)
                {
                    int server_refresh_rate = frequency < 50 ? 50 : frequency;
                    websocket = new WebSocket(String.Format("{0}/stream/depthandcircles/{1}/", path, server_refresh_rate));
                    websocket.EnableAutoSendPing = true;
                    websocket.MessageReceived += Websocket_MessageReceived;
                    websocket.Open();
                    while (websocket.State == WebSocketState.Connecting) { }
                }
                while (websocket.State == WebSocketState.Open)
                {
                    if (buffer != null)
                    {
                        Color[] colors = null;
                        var pointaray = depthArrayToPointArray(buffer, palette, out colors, xscale, yscale, dscale);
                        var m = createMesh(pointaray, colors, crect, dscale, distance);
                        return new Tuple<GH_Mesh, Curve[], List<GH_Circle>>(m.Item1, m.Item2, circles);
                    }
                }
            }
            return new Tuple<GH_Mesh, Curve[], List<GH_Circle>>(null, null, null);
        }

        /// <summary>
        /// Triggere when receiving websocket messages
        /// Using text/messages on purpose because binary payloads failed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Websocket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(e.Message);
            buffer = Convert.FromBase64String((string)payload["d"]);
            circles = new List<GH_Circle>();
            if (payload["c"] != null)
            {
                foreach (IDictionary<string, Newtonsoft.Json.Linq.JToken> dict in (ICollection<Newtonsoft.Json.Linq.JToken>)payload["c"])
                {
                    var pt = new Point3d(dict["x"].ToObject<int>(), dict["y"].ToObject<int>(), dict["z"].ToObject<int>());
                    circles.Add(new GH_Circle(new Circle(pt, dict["r"].ToObject<int>())));
                }
            }                
        }

        /// <summary>
        /// Convert the byte depth array to an array of points with X,Y,Z coordinates
        /// And populate an array of colors for coloring each point
        /// </summary>
        /// <param name="deptharray">byte depth array</param>
        /// <param name="palette">color palette holding 255 colors</param>
        /// <param name="colors">array of matching colors for each point</param>
        /// <param name="scale">planar scane in x and y direction</param>
        /// <param name="dscale">depth scale</param>
        /// <returns>array of points</returns>
        private Point3f[] depthArrayToPointArray(byte[] deptharray, List<GH_Colour> palette, out Color[] colors, double xscale, double yscale, double dscale)
        {
            int depthPoint;
            var points = new Point3f[640 * 480];
            colors = new Color[640 * 480];
            var p = new Point3f();
            var i = 0;

            for (var rows = 0; rows < 480; rows++)
            {
                for (var columns = 0; columns < 640; columns++)
                {
                    depthPoint = Convert.ToInt32(deptharray[i]);
                    p.X = (float)(columns * xscale);
                    p.Y = (float)(rows * yscale);
                    p.Z = (float)(depthPoint * dscale);
                    if (palette != null)
                    {
                        colors[i] = palette[depthPoint].Value;
                    }
                    points[i] = p;
                    i++;
                }
            }
            return points;
        }

        /// <summary>
        /// Create a mesh from point array and color array which is optionally cropped
        /// </summary>
        /// <param name="vertices">point array</param>
        /// <param name="colors">color array (matching size to points)</param>
        /// <param name="rect">rectangle cropping the view</param>
        /// <returns>mesh</returns>
        private Tuple<GH_Mesh, Curve[]> createMesh(Point3f[] vertices, Color[] colors, GH_Rectangle rect, double dscale,  double distance)
        {
            int xd = 640;
            Mesh mesh = new Mesh();
            mesh.Vertices.Capacity = vertices.Length;
            mesh.Vertices.UseDoublePrecisionVertices = false;
            mesh.Vertices.AddVertices(vertices);
            mesh.VertexColors.SetColors(colors);
            int start_y = 1, start_x = 1, max_y = 480, max_x = 640;
            if (rect != null)
            {
                applyIfValueInRange((int)rect.Value.PointAt(0, 0).Y, max_y, ref start_y);
                applyIfValueInRange((int)rect.Value.PointAt(0, 0).X, max_x, ref start_x);
                applyIfValueInRange((int)rect.Value.PointAt(1, 1).Y, max_y, ref max_y);
                applyIfValueInRange((int)rect.Value.PointAt(1, 1).X, max_x, ref max_x);
            }
            for (int y = start_y; y < max_y - 1; y++)
            {
                for (int x = start_x; x < max_x - 1; x++)
                {
                    int i = y * xd + x;
                    int j = (y - 1) * xd + x;
                    mesh.Faces.AddFace(j - 1, j, i, i - 1);
                }
            }

            Curve[] curves = null;
            if (distance > 0.0)
            {
                Point3d p1 = new Point3d(0, 0, 0);
                Point3d p2 = new Point3d(0, 0, 255 * dscale);
                curves = Mesh.CreateContourCurves(mesh, p1, p2, distance);
            }

            return new Tuple<GH_Mesh, Curve[]>(new GH_Mesh(mesh), curves);
        }

        /// <summary>
        /// Set referenced variable to int value if within range
        /// </summary>
        /// <param name="value">new int value</param>
        /// <param name="max">lower bound is zero, max is upper bound</param>
        /// <param name="result">int to overwrite</param>
        private void applyIfValueInRange(int value, int max, ref int result)
        {
            if (value > 0 && value <= max)
            {
                result = value;
            }
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
