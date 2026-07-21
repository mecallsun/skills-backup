using System;
using BCrypt.Net;
Console.WriteLine(BCrypt.Net.BCrypt.HashPassword(args[0], 11));