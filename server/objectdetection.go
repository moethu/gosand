package main

import (
	"log"

	"gocv.io/x/gocv"
)

type circle struct {
	X int `json:"x"`
	Y int `json:"y"`
	R int `json:"r"`
}

type config struct {
	Dp      float64 `json:"dp"`
	Mindist float64 `json:"mindist"`
	Param1  float64 `json:"param1"`
	Param2  float64 `json:"param2"`
	Min     int     `json:"min"`
	Max     int     `json:"max"`
}

func detectCircles(cfg config) []circle {
	frame := freenect_device.RGBAFrame()
	img, err := gocv.ImageToMatRGBA(frame)
	if err != nil {
		log.Println(err)
		return []circle{}
	}
	defer img.Close()

	gocv.MedianBlur(img, &img, 5)

	cimg := gocv.NewMat()
	defer cimg.Close()

	gocv.CvtColor(img, &cimg, gocv.ColorRGBAToGray)

	circles := gocv.NewMat()
	defer circles.Close()

	defaultconfig := config{Dp: 1, Mindist: float64(img.Rows() / 8), Param1: 75, Param2: 20, Min: 1, Max: 0}
	if (config{}) == cfg {
		cfg = defaultconfig
	}

	gocv.HoughCirclesWithParams(
		cimg,
		&circles,
		gocv.HoughGradient,
		cfg.Dp,      // dp
		cfg.Mindist, // minDist
		cfg.Param1,  // param1 75
		cfg.Param2,  // param2 20
		cfg.Min,     // minRadius 10
		cfg.Max,     // maxRadius 0
	)

	var cs []circle

	for i := 0; i < circles.Cols(); i++ {
		v := circles.GetVecfAt(0, i)
		if len(v) > 2 {
			x := int(v[0])
			y := int(v[1])
			r := int(v[2])
			cs = append(cs, circle{X: x, Y: y, R: r})
		}
	}

	return cs
}
