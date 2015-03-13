StringSocket
============

Wrapper around C# Socket class that allows easy sending and receiving of strings.
By Elliot Hatch and Samuel Davidson. Used in Boogle.

Stores received messages in an internal buffer and calls a passed delegate function when a complete
string has been received.
