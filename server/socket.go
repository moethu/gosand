package main

import (
	"encoding/json"
	"log"
	"time"

	"github.com/gin-gonic/gin"
	"github.com/gorilla/websocket"
	"github.com/moethu/gosand/server/freenect"
)

const (
	writeTimeout   = 10 * time.Second
	readTimeout    = 60 * time.Second
	pingPeriod     = (readTimeout * 9) / 10
	maxMessageSize = 512
)

// Client holding socket and channels
type Client struct {

	// The websocket connection.
	conn *websocket.Conn

	// Buffered channels messages.
	write  chan []byte // images and data to client
	read   chan []byte // commands from client
	closed bool        // closed by peer
}

type payload struct {
	Depthframe []byte   `json:"d"`
	Circles    []circle `json:"c"`
}

// streamReader reads messages from the websocket connection and fowards them to the read channel
func (c *Client) streamReader() {
	defer func() {
		c.conn.Close()
	}()
	c.conn.SetReadLimit(maxMessageSize)
	c.conn.SetReadDeadline(time.Now().Add(readTimeout))
	// SetPongHandler sets the handler for pong messages received from the peer.
	c.conn.SetPongHandler(func(string) error { c.conn.SetReadDeadline(time.Now().Add(readTimeout)); return nil })
	for {
		_, message, err := c.conn.ReadMessage()
		if err != nil {
			if websocket.IsUnexpectedCloseError(err, websocket.CloseGoingAway, websocket.CloseAbnormalClosure) {
				log.Printf("error: %v", err)
				c.closed = true
			}
			break
		}
		// feed message to command channel
		c.read <- message
	}
}

// streamWriter writes messages from the write channel to the websocket connection
func (c *Client) streamWriter() {
	ticker := time.NewTicker(pingPeriod)
	defer func() {
		ticker.Stop()
		c.conn.Close()
	}()
	for {
		// Go’s select lets you wait on multiple channel operations.
		// We’ll use select to await both of these values simultaneously.
		select {
		case message, ok := <-c.write:
			c.conn.SetWriteDeadline(time.Now().Add(writeTimeout))
			if !ok {
				c.conn.WriteMessage(websocket.CloseMessage, []byte{})
				return
			}

			// NextWriter returns a writer for the next message to send.
			// The writer's Close method flushes the complete message to the network.
			w, err := c.conn.NextWriter(websocket.TextMessage)
			if err != nil {
				return
			}
			w.Write(message)

			// Add queued messages to the current websocket message
			n := len(c.write)
			for i := 0; i < n; i++ {
				w.Write(<-c.write)
			}

			if err := w.Close(); err != nil {
				return
			}

		//a channel that will send the time with a period specified by the duration argument
		case <-ticker.C:
			// SetWriteDeadline sets the deadline for future Write calls
			// and any currently-blocked Write call.
			// Even if write times out, it may return n > 0, indicating that
			// some of the data was successfully written.
			c.conn.SetWriteDeadline(time.Now().Add(writeTimeout))
			if err := c.conn.WriteMessage(websocket.PingMessage, nil); err != nil {
				return
			}
		}
	}
}

// ServeWebsocket godoc
// @Summary Serves a websocket streaming kinect frames
// @Description Serves frames continuously
// @Accept  json
// @Produce  jpeg
// @Param type path string true "Frame Type deptharray, depthframe, irframe, rgbframe"
// @Param time path int true "Image sending frequency in ms"
// @Success 200 byte jpeg
// @Router /stream/{type}/{time}/ [get]
func ServeWebsocket(c *gin.Context) {

	// upgrade connection to websocket
	conn, err := upgrader.Upgrade(c.Writer, c.Request, nil)
	if err != nil {
		log.Println(err)
		return
	}
	conn.EnableWriteCompression(false)

	// create two channels for read write concurrency
	cWrite := make(chan []byte)
	cRead := make(chan []byte)

	client := &Client{conn: conn, write: cWrite, read: cRead, closed: false}

	circleDetection := false
	if c.Request.URL.Query().Get("detection") != "" {
		circleDetection = true
	}

	if freenect_device_present {
		freenect_device.SetLed(freenect.LED_BLINK_RED_YELLOW)
	}

	wait_time, err := time.ParseDuration(c.Params.ByName("time") + "ms")
	if err != nil {
		wait_time, _ = time.ParseDuration("200ms")
	}
	go client.render(circleDetection, wait_time)

	// run reader and writer in two different go routines
	// so they can act concurrently
	go client.streamReader()
	go client.streamWriter()
}

func (c *Client) render(circleDetection bool, wait_time time.Duration) {
	for {
		depth_array := freenect_device.DepthArray(true)
		var cs []circle
		if circleDetection {
			cs = detectCircles(circleDetectionConfig)
			for i, circle := range cs {
				depth := depth_array[int(circle.X*480+circle.Y)]
				cs[i].Z = int(depth)
			}
		}
		p := payload{Depthframe: depth_array, Circles: cs}
		b, err := json.Marshal(p)
		if err != nil {
			log.Println(err)
		} else {
			c.write <- b
		}
		time.Sleep(wait_time)

		if c.closed {
			if freenect_device_present {
				freenect_device.SetLed(freenect.LED_OFF)
			}
			return
		}
	}
}
