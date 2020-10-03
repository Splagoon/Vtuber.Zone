# Vtuber.Zone

This repository contains all of the source code and assets for [Vtuber.Zone](https://vtuber.zone), a website that aims to list all live Vtuber streams.

## Running locally
### Frontend

To build the website, [Node.js](https://nodejs.org/en/download/) is needed. Inside the "Website" folder, run `npm install` to install dependencies, and `npm run dev` to start a local instance for development. Run `npm run build` to generate a production-ready version of the website in the "public" folder.

### Backend

All of the services (Twitch, Twitter, YouTube, and WebAPI) use the [.NET Core SDK](https://dotnet.microsoft.com/download). In the relevant project folders, use `dotnet test` to run the unit tests and `dotnet run` to launch the service.

For the services to work correctly, some secret values must be set. Copy [secrets.example.json](secrets.example.json) to "secrets.json" and enter the relevant API keys and connection strings.

## Contributing

Contributions are welcome! Please submit a PR if you have one. All the Vtubers go in the [settings.json](settings.json) file.

## License

Unless otherwise specified, all code in this repository is made available under the terms of the [ISC License](LICENSE).
