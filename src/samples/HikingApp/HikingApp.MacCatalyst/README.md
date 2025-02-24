# HikingApp MacCatalyst

HikingApp is a MacCatalyst application designed to help users explore and purchase hiking trails. The app provides detailed information about various trails, including descriptions, distances, difficulty levels, and terrain types. Users can also sign in using their Apple ID.

The goal of this project is to prototype and validate Swift bindings in e2e manner.

## Requirements
The Mac Catalyst workload: `dotnet workload install maccatalyst`

## How to run

**Terminal**: dotnet build t:Run

**VSCode**: Install .NET MAUI extension https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.dotnet-maui and hit `F5`. (only available when the app folder is opened in separate VSCode instance)

The output files get generated into *bin* and *obj* folders.

## TODOs
  - [ ] Sign-in functionality using AppleID
  - [ ] In-app trail purchases
  - [ ] CI job validating build correctness of the HikingApp
