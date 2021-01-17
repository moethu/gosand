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
    /// Average Mesh Component for Gosand
    /// </summary>
    public class GosandAvgMesh : GH_Component
    {
        public GosandAvgMesh() : base("Average Mesh Points", "AverageMesh", "Collects a set of meshes and builds a new mesh with average Z values", "Gosand", "Device") { }
        
        /// <summary>
        /// Register Input Ports
        /// </summary>
        /// <param name="pManager"></param>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Number of meshes", "n", "Number of meshes o collect.", GH_ParamAccess.item);
            pManager.AddMeshParameter("Mesh", "M", "Input single meshes here over time.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Reset", "R", "Connect a button to reset the mesh buffer.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Dimension X", "x", "New mesh dimension in X.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Dimension Y", "y", "New mesh dimension in Y.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Register Output Ports
        /// </summary>
        /// <param name="pManager"></param>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Mesh", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Number of Meshes", "c", "Number of meshes currently buffered", GH_ParamAccess.item);
        }

        /// <summary>
        /// Mesh buffer
        /// </summary>
        private Dictionary<int, Mesh> buffer;

        /// <summary>
        /// To keep Mesh once it has been created
        /// </summary>
        private GH_Mesh bufferedMesh;

        /// <summary>
        /// Triggered when solving the instance
        /// </summary>
        /// <param name="DA"></param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Integer count = new GH_Integer();
            DA.GetData<GH_Integer>(0, ref count);
            GH_Mesh mesh = new GH_Mesh();
            DA.GetData<GH_Mesh>(1, ref mesh);
            GH_Boolean reset = new GH_Boolean();
            DA.GetData<GH_Boolean>(2, ref reset);
            GH_Integer xd = new GH_Integer();
            DA.GetData<GH_Integer>(3, ref xd);
            GH_Integer yd = new GH_Integer();
            DA.GetData<GH_Integer>(4, ref yd);

            if (buffer == null || reset.Value)
            {
                buffer = new Dictionary<int, Mesh>();
                bufferedMesh = null;
            }

            if (buffer.Count < count.Value)        
            {
                    if (mesh != null && mesh.Value != null)
                    {
                        int id = mesh.GetHashCode();
                        if (!buffer.ContainsKey(id))
                        {
                            buffer.Add(id, mesh.Value);
                        }
                    }
            }
            else
            {
                if (bufferedMesh == null)
                {
                    bufferedMesh = new GH_Mesh(averageMesh(xd.Value, yd.Value));
                }
            }

            DA.SetData(0, bufferedMesh);
            DA.SetData(1, buffer.Count);
        }

        /// <summary>
        /// Get Point X/Y Identifier as string
        /// </summary>
        /// <param name="p">Point</param>
        /// <returns></returns>
        private string getPointGeoId(Point3d p)
        {
            return String.Format("{0}/{1}", p.X, p.Y);
        }

        /// <summary>
        /// Builds a new Mesh using average Z values
        /// </summary>
        /// <param name="dx">Dimension in X</param>
        /// <param name="dy">Dimension in Y</param>
        /// <returns>Mesh</returns>
        private Mesh averageMesh(int dx, int dy)
        {
            Dictionary<string, List<double>> pointbuffer = new Dictionary<string, List<double>>();

            foreach (Mesh m in buffer.Values)
            {
                foreach (Point3d p in m.Vertices)
                {
                    string id = getPointGeoId(p);
                    if (!pointbuffer.ContainsKey(id))
                    {
                        pointbuffer.Add(id, new List<double>());
                    }
                    pointbuffer[id].Add(p.Z);
                }
            }

            List<Point3d> vertices = new List<Point3d>();
            foreach (string key in pointbuffer.Keys)
            {
                string[] positionvalues = key.Split('/');
                double Z = pointbuffer[key].Average();
                vertices.Add(new Point3d(double.Parse(positionvalues[0]), double.Parse(positionvalues[1]), Z));
            }

            Mesh mesh = new Mesh();
            mesh.Vertices.Capacity = vertices.Count;
            mesh.Vertices.UseDoublePrecisionVertices = false;
            mesh.Vertices.AddVertices(vertices);
            
            int start_y = 1, start_x = 1, max_y = dy, max_x = dx;
            for (int y = start_y; y < max_y - 1; y++)
            {
                for (int x = start_x; x < max_x - 1; x++)
                {
                    int i = y * max_x + x;
                    int j = (y - 1) * max_x + x;
                    mesh.Faces.AddFace(j - 1, j, i, i - 1);
                }
            }

            return mesh;
        }

        public override Guid ComponentGuid
        {
            get
            {
                return new Guid("{47ccfff4-187d-4671-9193-aca29d3733e9}");
            }
        }

        protected override Bitmap Icon => gosand.Properties.Resources.mesh;
    }
}
