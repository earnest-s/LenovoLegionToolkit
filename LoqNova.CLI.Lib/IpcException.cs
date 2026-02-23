using System;

namespace LoqNova.CLI.Lib;

public class IpcException(string? name) : Exception(name);
