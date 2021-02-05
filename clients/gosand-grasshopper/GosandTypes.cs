namespace gosand
{
    class Payload
    {
        [JsonProperty("d")]
        public string Depthframe;

        [JsonProperty("c")]
        public GosandCircle[] Circles;
       
    }

    class Circle
    {
        [JsonProperty("x")]
        public int X;
        [JsonProperty("y")]
        public int Y;
        [JsonProperty("z")]
        public int Z;
        [JsonProperty("r")]
        public int Radius;

        public Rhino.Geometry.Point3d ToPoint3d(Point3d scale){
            return new Rhino.Geometry.Point3d(this.X * scale.X, this.Y * scale.Y, this.Z * scale.Z);
        }

        public Rhino.Geometry.Cirlce ToCircle(Point3d scale){
            return new Rhino.Geometry.Circle(this.ToPoint3d(scale), this.Radius);
        }
    }

    class GosandData
    {

        public GH_Mesh Mesh;
        public Curve[] Curves;
        public List<GH_Circle> Circles;
        public Point3d Scale;

        public bool HasData(){
            return this.Mesh != null;
        }

        public GosandData()
        {}

        public static GosandData FromResponseString(string response, List<GH_Colour> palette, Point3d scale, GH_Rectangle rect, double distance)
        {   
            GosandData d = new GosandData();
            d.Scale = scale;

            Payload p = Newtonsoft.Json.JsonConvert.DeserializeObject<Payload>(response);
            var buffer = Convert.FromBase64String((string)p.Depthframe);
            d.Circles = new List<GH_Circle>();
            if (p.Circles != null)
            {
                foreach (var circle in p.Circles)
                {
                    d.Circles.Add(new GH_Circle(circle.ToCircle(scale)));
                }
            }

            Color[] colors = null;
            var pointarray = d.depthArrayToPointArray(buffer, palette, out colors);
            var m = createMesh(pointarray, colors, crect, distance);
            this.Mesh = m.Item1;
            this.Curves = m.Item2;
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
        private Point3f[] depthArrayToPointArray(byte[] deptharray, List<GH_Colour> palette, out Color[] colors)
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
                    p.X = (float)(columns * this.Scale.X);
                    p.Y = (float)(rows * this.Scale.Y);
                    p.Z = (float)(depthPoint * this.Scale.Z);
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
        private Tuple<GH_Mesh, Curve[]> createMesh(Point3f[] vertices, Color[] colors, GH_Rectangle rect, double distance)
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
                this.applyIfValueInRange((int)rect.Value.PointAt(0, 0).Y, max_y, ref start_y);
                this.applyIfValueInRange((int)rect.Value.PointAt(0, 0).X, max_x, ref start_x);
                this.applyIfValueInRange((int)rect.Value.PointAt(1, 1).Y, max_y, ref max_y);
                this.applyIfValueInRange((int)rect.Value.PointAt(1, 1).X, max_x, ref max_x);
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
                Point3d p2 = new Point3d(0, 0, 255 * scale.Z);
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



    }
}