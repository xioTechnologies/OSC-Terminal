# Rug.Osc 

Simple, complete, open source OSC implementation for .NET and Mono. 

# Key Features: 
* Pure .NET (C#) (2.0 or greater) so any .NET / Mono platform is supported. (Windows, OS-X, Linux, Android, I-OS and more)
* Message argument types supported are: **int**, **long**, **float**, **double**, **string**, **symbol**, **bool**, **RGBA**, **Osc-Null**, **Osc-Timetag**, **Osc-Midi**, **impulse**, **char**, **blob** and **arrays**.
* Message address patterns and pattern matching.
* Osc message bundles.
* Send and receive osc packets via UDP.
* Supports IPv4, IPv6, Unicast, Multicast and Broadcast (IPv4 only)
* Read and write osc packets to streams.
* Parse osc messages and bundles from strings.
* Optimized for use in real-time applications.
* 100% thread safe.
* Released under a permissive [MIT License](https://bitbucket.org/rugcode/rug.osc/wiki/License)




# Send Example

```
#!c#

IPAddress address = IPAddress.Parse("127.0.0.1"); 
int port = 12345;

using (OscSender sender = new OscSender(address, port)) 
{
	sender.Connect();

	sender.Send(new OscMessage("/test", 1, 2, 3, 4));
}

```


# More Examples
* [Sending a message](https://bitbucket.org/rugcode/rug.osc/wiki/Sending%20a%20message)
* [Receiving a message](https://bitbucket.org/rugcode/rug.osc/wiki/Receiving%20a%20message)



# Links
* [Source distribution](https://bitbucket.org/rugcode/rug.osc/get/Version-1.2.4.0.zip) (includes examples)
* [Binarys](https://bitbucket.org/rugcode/rug.osc/downloads/Rug.Osc%201.2.4.0.zip)
* [Nuget project page](http://www.nuget.org/packages/Rug.Osc/)
* If you use this project please rate it on [Ohloh](https://www.ohloh.net/p/rugosc)
* Feel free to follow us on twitter [@RugCode](https://twitter.com/RugCode)