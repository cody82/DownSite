DownSite is a combined CMS and Static Site Generator. (Example site is on https://cody82.github.io)
It features a webserver based on ServiceStack and a generator that creates static html files that can be served by e.g. Apache.

It is easier to use than e.g. Jekyll and Wordpress as it can be edited in the browser and does not need a webserver.
Also it can embed YouTube/Vimeo and convert/scale uploaded videos.

##How to use
* Download a release/source and start the DownSite project.
* Point your browser to "http://<your_computer_name>:1337/". An example website shows up.
* Point your browser to "http://<your_computer_name>:1337/admin.html" and login with username "downsite" and password "admin".
* You can now start editing the website.
* Press <ENTER> in the console window to generate a static website. The output will be placed in the "output" director.

##Features
* Images are automatically scaled down and will be displayed in the right resolution according to screen size in newer browsers.
* Rescale videos with FFmpeg. When you upload a 1080p video it will be reencoded to 480p and 720p. FFmpeg has to be installed seperately.
* Write your pages in Markdown (or HTML) with preview (left side Markdown, right side preview).

##Supported Operating Systems
* Windows/.NET
* Linux/Mono

##Webserver Proxy
###Apache
TODO
###Lighttpd
TODO
