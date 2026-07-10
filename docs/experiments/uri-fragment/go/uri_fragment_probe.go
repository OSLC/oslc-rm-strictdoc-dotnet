package main

import (
	"fmt"
	"net/url"
	"reflect"
)

func main() {
	affectedBy := "http://open-services.net/ns/rm#affectedBy"
	trackedBy := "http://open-services.net/ns/rm#trackedBy"

	urlA, err := url.Parse(affectedBy)
	if err != nil {
		panic(err)
	}
	urlB, err := url.Parse(trackedBy)
	if err != nil {
		panic(err)
	}

	fmt.Println("Go net/url.URL")
	fmt.Printf("type=%T\n", *urlA)
	fmt.Printf("a=%s fragment=%s\n", urlA.String(), urlA.Fragment)
	fmt.Printf("b=%s fragment=%s\n", urlB.String(), urlB.Fragment)
	fmt.Printf("reflect.DeepEqual(a,b)=%v\n", reflect.DeepEqual(urlA, urlB))
	fmt.Printf("*a == *b=%v\n", *urlA == *urlB)

	valueSet := map[url.URL]struct{}{*urlA: {}, *urlB: {}}
	stringSet := map[string]struct{}{urlA.String(): {}, urlB.String(): {}}
	pointerSet := map[*url.URL]struct{}{urlA: {}, urlB: {}}
	fmt.Printf("map[url.URL] count=%d\n", len(valueSet))
	fmt.Printf("map[*url.URL] count=%d\n", len(pointerSet))
	fmt.Printf("map[string] count=%d\n", len(stringSet))
}

