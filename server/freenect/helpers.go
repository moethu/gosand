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

// Helper methods for the freenect package.
//
// Author: Adam Jensen <acj@linuxguy.org>

package freenect

import (
	"image/jpeg"
	"os"
)

func SaveRGBAFrame(d *FreenectDevice, filename string) {
	img := d.RGBAFrame()

	toimg, _ := os.Create(filename)
	defer toimg.Close()

	jpeg.Encode(toimg, img, &jpeg.Options{jpeg.DefaultQuality})
}

func SaveIRFrame(d *FreenectDevice, filename string) {
	img := d.IRFrame()

	toimg, _ := os.Create(filename)
	defer toimg.Close()

	jpeg.Encode(toimg, img, &jpeg.Options{jpeg.DefaultQuality})
}

func SaveDepthFrame(d *FreenectDevice, filename string) {
	img := d.DepthFrame()

	toimg, _ := os.Create(filename)
	defer toimg.Close()

	jpeg.Encode(toimg, img, &jpeg.Options{jpeg.DefaultQuality})
}
