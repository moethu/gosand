
<img src="https://raw.githubusercontent.com/moethu/gosand/main/images/logo.png" height="50">

Gosand is a simple webserver serving a Kinects data using gonect - a Go wrapper for the [libfreenect](https://github.com/OpenKinect/libfreenect) library.

![](https://raw.githubusercontent.com/moethu/gosand/main/images/concept.png)

Why? I started building a magical sandbox - which is basically a box full of sand and a depth camera scanning its topography. So I could see changes on the physical model within milliseconds in a 3D model in Rhino where it can be analyzed instantly. For this I needed to get depth frames from the Kinect Camera into Rhino 3D running on a Windows. That's easy if you are a bare metal Windows user but not if you are running Windows on a VM on a Linux Host. So I decided to simply use a raspberrypi to serve the Kinects Data via HTTP. So in the end you can connect the Kinect to a raspberry-pi and stream its data through a websocket to any 3D application running on any platform.

- Server: Go HTTP Server running on Linux
- Client: C#.NET Component for Rhino/Grasshopper
- Hardware: Kinect for XBOX 360 ModelNr. 1414, Kinect PC USB Cable, Raspberry pi 3 Model B+, a box full of sand.

## Server Installation and Usage

First, be sure that you have installed [libfreenect](https://github.com/OpenKinect/libfreenect).
Once the library is installed, simply `go run .` or build and run. All dependencies will be installed via go modules.
Note: I used a Raspi 3 Model B+, just make sure you are using a proper power supply for the raspi and no USB Charger.

Once the server is running you can simply test it by opening root: http://localhost:4777/
which renders a webGL scene showing the pointcloud of what the camera currently sees

![](https://raw.githubusercontent.com/moethu/gosand/main/images/home.png)

### Building freenect yourself

If you are experiencing any trouble or you've got only an outdated freenect version available you can also just build it yourself:

1. Get your build env ready
```
sudo apt-get install cmake libudev0 libudev-dev freeglut3 freeglut3-dev libxmu6 libxmu-dev libxi6 libxi-dev
```

2. Build libusb yourself
```
mkdir ~/src
cd ~/src
wget https://github.com/libusb/libusb/releases/download/v1.0.20/libusb-1.0.20.tar.bz2
tar -jxf libusb-1.0.20.tar.bz2
cd ~/src/libusb-1.0.20
./configure
make
sudo make install
```

3. Download and build freenect
```
cd ~/src
wget https://github.com/OpenKinect/libfreenect/archive/v0.5.3.tar.gz
tar -xvzf v0.5.3.tar.gz
cd ~/src/libfreenect-0.5.3
mkdir build
cd build
cmake -L ..
make
sudo make install
```

## Clients

![](https://raw.githubusercontent.com/moethu/gosand/main/images/example.png)

Currently there is only one client available: A Grasshopper 3D Component for McNeel's Rhino 3D. A Windows build of this component can be downloaded in "releases". Or you build it yourself using Visual Studio. All dependencies will be installed via nuget, except for Rhino and Grasshopper of course. The Component allows you to either stream depth frames into Grasshopper (for high frequency updates) or GET depth frame data via HTTP requests (recommended for low frequency updates). The component will instantly provide a 3D Mesh of the depth frame. 

## Sand

As a source of sand I recommend using kinetic sand or modelling sand which is basically quarz sand mixed with Polydimethylsiloxan (PDMS, 500 cst) on a ratio of 98:2. This mixture creates sand that looks like it is constantly wet and gives it great modelling performance. Mix it yourself at your own risk or simply purchase kinetic sand online but it is quite pricy in comparison.
