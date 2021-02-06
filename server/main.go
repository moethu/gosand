package main

import (
	"encoding/json"
	"html/template"
	"image"
	"image/jpeg"
	"io/ioutil"
	"log"

	"context"
	"flag"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/moethu/gosand/server/freenect"

	"github.com/gin-gonic/gin"
	"github.com/gorilla/websocket"
	_ "github.com/moethu/gosand/server/docs"
	swaggerFiles "github.com/swaggo/files"
	ginSwagger "github.com/swaggo/gin-swagger"
)

var freenect_device *freenect.FreenectDevice
var led_sleep_time time.Duration
var image_quality = 100
var freenect_device_present = false

// @title Gosand Server API
// @version 0.5
// @description This API provides access to a connected MS Kinect Camera.

// @contact.name API Support
// @contact.url http://github.com/moethu/gosand
func main() {
	flag.Parse()
	log.SetFlags(0)
	circleDetectionConfig = config{}
	led_sleep_time, _ = time.ParseDuration("200ms")

	freenect_device = freenect.NewFreenectDevice(0)

	if freenect_device.GetNumDevices() != 1 {
		log.Println("no single kinect device found. Starting in debug mode only.")
		freenect_device_present = false
	} else {
		ledStartup(freenect_device)
		freenect_device_present = true
	}

	router := gin.Default()
	port := ":4777"
	srv := &http.Server{
		Addr:         port,
		Handler:      router,
		ReadTimeout:  600 * time.Second,
		WriteTimeout: 600 * time.Second,
	}

	router.Static("/static/", "./static/")
	router.GET("/data/", GetArray)
	router.POST("/config/", PostCircles)
	router.GET("/frame/:type/", GetFrame)
	router.Any("/stream/:time/", ServeWebsocket)
	router.GET("/", home)
	router.GET("/socket", socket)

	url := ginSwagger.URL("http://localhost:4777/swagger/doc.json")
	router.GET("/swagger/*any", ginSwagger.WrapHandler(swaggerFiles.Handler, url))

	log.Println("Starting HTTP Server on Port", port)

	go func() {
		if err := srv.ListenAndServe(); err != nil && err != http.ErrServerClosed {
			log.Fatalf("listen: %s\n", err)
		}
	}()

	quit := make(chan os.Signal, 1)
	signal.Notify(quit, syscall.SIGINT, syscall.SIGTERM)
	<-quit
	log.Println("Shutdown Server")
	if freenect_device_present {
		ledShutdown(freenect_device)
		freenect_device.Stop()
		freenect_device.Shutdown()
	}
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()

	if err := srv.Shutdown(ctx); err != nil {
		log.Fatal("Server Shutdown: ", err)
	}
	log.Println("Server exiting")
}

func ledStartup(d *freenect.FreenectDevice) {
	d.SetLed(freenect.LED_YELLOW)
	time.Sleep(led_sleep_time)
	d.SetLed(freenect.LED_GREEN)
	time.Sleep(led_sleep_time)
	d.SetLed(freenect.LED_OFF)
}

func ledShutdown(d *freenect.FreenectDevice) {
	d.SetLed(freenect.LED_YELLOW)
	time.Sleep(led_sleep_time)
	d.SetLed(freenect.LED_RED)
	time.Sleep(led_sleep_time)
	d.SetLed(freenect.LED_OFF)
}

// GetFrame godoc
// @Summary Get Frame from Kinect
// @Description gets the current frame
// @Accept  json
// @Produce  jpeg
// @Param type path string true "Frame Type depth, ir or rgb"
// @Success 200 byte jpeg
// @Failure 404 {object} string
// @Router /frame/{type}/ [get]
func GetFrame(c *gin.Context) {
	freenect_device.SetLed(freenect.LED_GREEN)
	var img image.Image
	switch c.Params.ByName("type") {
	case "depth":
		img = freenect_device.DepthFrame()
		break
	case "ir":
		img = freenect_device.IRFrame()
		break
	case "rgb":
		img = freenect_device.RGBAFrame()
		break
	default:
		c.Data(404, "", nil)
		return
	}
	c.Writer.Header().Set("Content-Type", "image/jpeg")
	jpeg.Encode(c.Writer, img, &jpeg.Options{Quality: image_quality})
	freenect_device.SetLed(freenect.LED_OFF)
}

// GetArray godoc
// @Summary Get Depth Array
// @Description gets the current frames depth array
// @Accept  json
// @Produce  json
// @Success 200 {array} byte
// @Router /deptharray/ [get]
func GetArray(c *gin.Context) {
	cdetection := c.Request.URL.Query().Get("detection")
	freenect_device.SetLed(freenect.LED_GREEN)
	depth_array := freenect_device.DepthArray(true)

	var cs []circle
	if cdetection != "" {
		cs = detectCircles(circleDetectionConfig)
		for i, circle := range cs {
			depth := depth_array[int(circle.X*480+circle.Y)]
			cs[i].Z = int(depth)
		}
	}

	c.JSON(200, payload{Depthframe: depth_array, Circles: cs})
	freenect_device.SetLed(freenect.LED_OFF)
}

// PostCircles godoc
// @Summary Update OpenCV Circle Detection Config
// @Description returns OK
// @Accept  json
// @Produce  json
// @Success 200 {array} byte
// @Router /circles/ [post]
func PostCircles(c *gin.Context) {
	jsonData, err := ioutil.ReadAll(c.Request.Body)
	if err != nil {
		c.JSON(500, err)
	}
	var cfg config
	err = json.Unmarshal(jsonData, &cfg)
	if err != nil {
		c.JSON(500, err)
	}
	circleDetectionConfig = cfg
	c.JSON(200, "OK")
}

var upgrader = websocket.Upgrader{}

func home(c *gin.Context) {
	viewertemplate := template.Must(template.ParseFiles("templates/render.html"))
	viewertemplate.Execute(c.Writer, c.Request.Host)
}

func socket(c *gin.Context) {
	viewertemplate := template.Must(template.ParseFiles("templates/socket.html"))
	viewertemplate.Execute(c.Writer, c.Request.Host)
}
