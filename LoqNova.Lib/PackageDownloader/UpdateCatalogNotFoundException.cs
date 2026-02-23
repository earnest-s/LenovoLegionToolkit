using System;

namespace LoqNova.Lib.PackageDownloader;

public class UpdateCatalogNotFoundException(string? message, Exception? ex) : Exception(message, ex);
