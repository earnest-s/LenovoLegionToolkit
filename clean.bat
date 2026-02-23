@echo off

rmdir /s /q .vs
rmdir /s /q _ReSharper.Caches

rmdir /s /q build
rmdir /s /q build_installer

rmdir /s /q LoqNova.CLI\bin
rmdir /s /q LoqNova.CLI\obj

rmdir /s /q LoqNova.Lib\bin
rmdir /s /q LoqNova.Lib\obj

rmdir /s /q LoqNova.Lib.Automation\bin
rmdir /s /q LoqNova.Lib.Automation\obj

rmdir /s /q LoqNova.Lib.CLI\bin
rmdir /s /q LoqNova.Lib.CLI\obj

rmdir /s /q LoqNova.Lib.Macro\bin
rmdir /s /q LoqNova.Lib.Macro\obj

rmdir /s /q LoqNova.WPF\bin
rmdir /s /q LoqNova.WPF\obj

rmdir /s /q LoqNova.SpectrumTester\bin
rmdir /s /q LoqNova.SpectrumTester\obj
