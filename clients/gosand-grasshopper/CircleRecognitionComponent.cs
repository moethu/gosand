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
using System.Linq;

namespace gosand
{
    /// <summary>
    /// Circle Recognition Component for Gosand
    /// </summary>
    public class GosandCircRec : GH_Component
    {
        public GosandCircRec() : base("Circle Recognition", "CircleRecognition", "Updates OpenCV circle recognition config in a gosand server", "Gosand", "Device") { }

        /// <summary>
        /// Register Input Ports
        /// </summary>
        /// <param name="pManager"></param>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Url", "U", "Url to Gosand webserver. The url should only contain protocol, hostname and port like: http://localhost:4777", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Min Radius", "min", "Minium circle radius.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Max Radius", "max", "Maximum circle radius.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Dp", "dp", "Inverse ratio of the accumulator resolution to the image resolution.", GH_ParamAccess.item);
            pManager.AddNumberParameter("MinDist", "md", "Minimum distance between the centers of the detected circles.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Param1", "p1", "First method-specific parameter.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Param2", "p2", "Second method-specific parameter.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Update", "u", "Trigger to update server config", GH_ParamAccess.item);
        }

        /// <summary>
        /// Register Output Ports
        /// </summary>
        /// <param name="pManager"></param>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
           
        }

        /// <summary>
        /// Triggered when solving the instance
        /// </summary>
        /// <param name="DA"></param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_String path = new GH_String();
            DA.GetData<GH_String>(0, ref path);
            GH_Integer minrad = new GH_Integer();
            DA.GetData<GH_Integer>(1, ref minrad);
            GH_Integer maxrad = new GH_Integer();
            DA.GetData<GH_Integer>(2, ref maxrad);
            GH_Number dp = new GH_Number();
            DA.GetData<GH_Number>(3, ref dp);
            GH_Number mindist = new GH_Number();
            DA.GetData<GH_Number>(4, ref mindist);
            GH_Number param1 = new GH_Number();
            DA.GetData<GH_Number>(5, ref param1);
            GH_Number param2 = new GH_Number();
            DA.GetData<GH_Number>(6, ref param2);
            GH_Boolean trigger = new GH_Boolean();
            DA.GetData<GH_Boolean>(7, ref trigger);

            if (trigger.Value)
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(path.Value + "/circledetectionconfig/");
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    string json =
                    "{\"dp\":\"" + dp.Value + "\"," +
                    "\"mindist\":\"" + mindist.Value + "\"," +
                    "\"param1\":\"" + param1.Value + "\"," +
                    "\"param2\":\"" + param2.Value + "\"," +
                    "\"min\":\"" + minrad.Value + "\"," +
                    "\"max\":\"" + maxrad.Value + "\"}";
                    streamWriter.Write(json);
                }

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
        }

        public override Guid ComponentGuid
        {
            get
            {
                return new Guid("{47ccfff4-187d-4771-9173-aca29d3733e9}");
            }
        }

        protected override Bitmap Icon => gosand.Properties.Resources.mesh;
    }
}
