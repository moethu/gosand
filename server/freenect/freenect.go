/*
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

     http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

// freenect package, a wrapper for the libfreenect C library.
//
// Author: Adam Jensen <acj@linuxguy.org>

package freenect

/*
#cgo CFLAGS: -I/usr/local/include/libfreenect
#cgo LDFLAGS: -lfreenect -lfreenect_sync
#include <stdlib.h>
#include <stdio.h>
#include <libfreenect.h>
#include <libfreenect_sync.h>

freenect_raw_tilt_state* create_tilt_state() {
    freenect_raw_tilt_state *ts = malloc(sizeof(freenect_raw_tilt_state));
    return ts;
}

freenect_context * freenect_init_proxy() {
	freenect_context* f_ctx;
	freenect_init(&f_ctx, NULL);
    return f_ctx;
}

uint8_t get_byte(void *buf, int offset)
{
	return *((uint8_t *)buf + offset);
}

uint16_t get_byte_16(void *buf, int offset)
{
	return *((uint16_t *)buf + offset);
}
*/
import "C"

import (
	"image"
	"unsafe"
)

type FreenectDevice struct {
	DeviceIndex      int
	DeviceIndexCType C.int
	DeviceContext    *C.freenect_context
}

type TiltState struct {
	Accelerometer_x int16
	Accelerometer_y int16
	Accelerometer_z int16
	Tilt_angle      int8
	Tilt_status     TiltStatusCode
}

type TiltStatusCode uint

const (
	STOPPED                = 0 /* 0x00 */
	MOVEMENT_LIMIT         = 1 /* 0x01 */
	MOVING_TO_NEW_POSITION = 4 /* 0x04 */
)

type LedOptions uint

const (
	LED_OFF              = 0
	LED_GREEN            = 1
	LED_RED              = 2
	LED_YELLOW           = 3
	LED_BLINK_YELLOW     = 4
	LED_BLINK_GREEN      = 5
	LED_BLINK_RED_YELLOW = 6
)

type VideoFormat uint

const (
	FREENECT_VIDEO_RGB = 0 /**< Decompressed RGB mode (demosaicing
	done by libfreenect) */
	FREENECT_VIDEO_BAYER = 1 /**< Bayer compressed mode (raw
	information from camera) */
	FREENECT_VIDEO_IR_8BIT         = 2 /**< 8-bit IR mode  */
	FREENECT_VIDEO_IR_10BIT        = 3 /**< 10-bit IR mode */
	FREENECT_VIDEO_IR_10BIT_PACKED = 4 /**< 10-bit packed IR mode */
	FREENECT_VIDEO_YUV_RGB         = 5 /**< YUV RGB mode */
	FREENECT_VIDEO_YUV_RAW         = 6 /**< YUV Raw mode */
	//    FREENECT_VIDEO_DUMMY           = 2147483647 /**< Dummy value to force enum
	//												  to be 32 bits wide */
)

type DepthFormat uint

const (
	FREENECT_DEPTH_11BIT        = 0 /**< 11 bit depth information in one uint16_t/pixel */
	FREENECT_DEPTH_10BIT        = 1 /**< 10 bit depth information in one uint16_t/pixel */
	FREENECT_DEPTH_11BIT_PACKED = 2 /**< 11 bit packed depth information */
	FREENECT_DEPTH_10BIT_PACKED = 3 /**< 10 bit packed depth information */
	FREENECT_DEPTH_REGISTERED   = 4 /**< processed depth data in mm, aligned to 640x480 RGB */
	FREENECT_DEPTH_MM           = 5 /**< depth to each pixel in mm, but left unaligned to RGB image */
	//    FREENECT_DEPTH_DUMMY        = 2147483647 /**< Dummy value to force enum to be 32 bits wide */
)

func ConvertCTiltStructToGo(c_ts *C.freenect_raw_tilt_state) TiltState {
	ts := TiltState{
		int16(c_ts.accelerometer_x),
		int16(c_ts.accelerometer_y),
		int16(c_ts.accelerometer_z),
		int8(c_ts.tilt_angle),
		TiltStatusCode(int8(c_ts.tilt_status))}
	return ts
}

func ConvertGoTiltStructToC(ts TiltState) *C.freenect_raw_tilt_state {
	c_ts := C.create_tilt_state()
	c_ts.accelerometer_x = C.int16_t(ts.Accelerometer_x)
	c_ts.accelerometer_y = C.int16_t(ts.Accelerometer_y)
	c_ts.accelerometer_z = C.int16_t(ts.Accelerometer_z)
	c_ts.tilt_angle = C.int8_t(ts.Tilt_angle)
	c_ts.tilt_status = C.freenect_tilt_status_code(ts.Tilt_status)
	return c_ts
}

func NewFreenectDevice(device_index int) *FreenectDevice {
	return &FreenectDevice{device_index, C.int(device_index), initDeviceContext()}
}

func initDeviceContext() *C.freenect_context {
	return C.freenect_init_proxy()
}

func (d *FreenectDevice) RawRGBFrame(format VideoFormat) (unsafe.Pointer, uint32) {
	var data unsafe.Pointer
	var timestamp C.uint32_t
	out := C.freenect_sync_get_video(&data, &timestamp, d.DeviceIndexCType, C.freenect_video_format(format))
	if out > 0 {
		return nil, 0
	}
	return data, uint32(timestamp)
}

func (d *FreenectDevice) RawDepthFrame(format VideoFormat) (unsafe.Pointer, uint32) {
	var data unsafe.Pointer
	var timestamp C.uint32_t
	out := C.freenect_sync_get_depth(&data, &timestamp, d.DeviceIndexCType, C.freenect_depth_format(format))
	if out > 0 {
		return nil, 0
	}
	return data, uint32(timestamp)
}

func (d *FreenectDevice) RGBAFrame() *image.RGBA {
	data, _ := d.RawRGBFrame(FREENECT_VIDEO_RGB)

	r := image.Rect(0, 0, 640, 480)
	img := image.NewRGBA(r)

	for row := 0; row < 480; row++ {
		for col := 0; col < 640; col++ {
			targetPos := C.int(row*640*4 + col*4)
			sourcePos := C.int(row*640*3 + col*3)

			img.Pix[targetPos] = uint8(C.get_byte(data, sourcePos))
			img.Pix[targetPos+1] = uint8(C.get_byte(data, sourcePos+1))
			img.Pix[targetPos+2] = uint8(C.get_byte(data, sourcePos+2))
			img.Pix[targetPos+3] = 1
		}
	}

	img.Stride = 640 * 4

	return img
}

func (d *FreenectDevice) IRFrame() *image.RGBA {
	data, _ := d.RawRGBFrame(FREENECT_VIDEO_IR_8BIT)

	r := image.Rect(0, 0, 640, 480)
	img := image.NewRGBA(r)

	for row := 0; row < 480; row++ {
		for col := 0; col < 640; col++ {
			targetPos := C.int(row*640*4 + col*4)
			sourcePos := C.int(row*640 + col)

			val := uint8(C.get_byte(data, sourcePos))
			img.Pix[targetPos] = val
			img.Pix[targetPos+1] = val
			img.Pix[targetPos+2] = val
			img.Pix[targetPos+3] = 0xff
		}
	}

	img.Stride = 640 * 4

	return img
}

func (d *FreenectDevice) DepthFrame11Bit() *image.RGBA {

	data, _ := d.RawDepthFrame(FREENECT_DEPTH_11BIT)

	r := image.Rect(0, 0, 640, 480)
	img := image.NewRGBA(r)

	for row := 0; row < 480; row++ {
		for col := 0; col < 640; col++ {
			sourcePos := C.int(row*640 + col)
			targetPos := C.int(row*640*4 + col*4)
			val := uint16(C.get_byte_16(data, sourcePos))
			img.Pix[targetPos] = uint8(val / 2)
			img.Pix[targetPos+1] = uint8(val / 2)
			img.Pix[targetPos+2] = uint8(val / 2)
			img.Pix[targetPos+3] = 1
		}
	}

	img.Stride = 640 * 4

	return img
}

func (d *FreenectDevice) DepthFrame() *image.RGBA {

	data, _ := d.RawDepthFrame(FREENECT_DEPTH_REGISTERED)

	r := image.Rect(0, 0, 640, 480)
	img := image.NewRGBA(r)

	for row := 0; row < 480; row++ {
		for col := 0; col < 640; col++ {
			sourcePos := C.int(row*640*2 + col*2)
			targetPos := C.int(row*640*4 + col*4)
			val := uint8(C.get_byte(data, sourcePos))
			img.Pix[targetPos] = val
			img.Pix[targetPos+1] = val
			img.Pix[targetPos+2] = val
			img.Pix[targetPos+3] = 1
		}
	}

	img.Stride = 640 * 4

	return img
}

func (d *FreenectDevice) DepthArray() []byte {
	data, _ := d.RawDepthFrame(FREENECT_DEPTH_REGISTERED)

	result := make([]byte, 640*480)
	i := 0
	for row := 0; row < 480; row++ {
		for col := 0; col < 640; col++ {
			sourcePos := C.int(row*640*2 + col*2)
			val := uint8(C.get_byte(data, sourcePos))
			result[i] = val
			i++
		}
	}

	return result
}

func (d *FreenectDevice) GetTiltDegs(ts TiltState) float32 {
	c_ts := ConvertGoTiltStructToC(ts)
	return float32(C.freenect_get_tilt_degs(c_ts))
}

// Set the tilt angle (in degrees)
func (d *FreenectDevice) SetTiltDegs(degs int) uint {
	// TODO: Check result
	return uint(C.freenect_sync_set_tilt_degs(C.int(degs), d.DeviceIndexCType))
}

func (d *FreenectDevice) GetTiltState() TiltState {
	c_ts := (*C.freenect_raw_tilt_state)(C.create_tilt_state())
	C.freenect_sync_get_tilt_state(&c_ts, d.DeviceIndexCType)
	ts := ConvertCTiltStructToGo(c_ts)
	return ts
}

func (d *FreenectDevice) GetTiltStatus(ts TiltState) TiltStatusCode {
	return TiltStatusCode(C.freenect_get_tilt_status(ConvertGoTiltStructToC(ts)))
}

func (d *FreenectDevice) SetLed(color uint) {
	// TODO: Check result
	C.freenect_sync_set_led(C.freenect_led_options(color), d.DeviceIndexCType)
}

func (d *FreenectDevice) GetNumDevices() uint {
	return uint(C.freenect_num_devices(d.DeviceContext))
}

func (d *FreenectDevice) Stop() {
	C.freenect_sync_stop()
}

func (d *FreenectDevice) Shutdown() {
	C.freenect_shutdown(d.DeviceContext)
}
