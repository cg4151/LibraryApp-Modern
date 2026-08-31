# Modern Library Management App

A modern, simplified library management application with **Koha integration** and **RFID gate support**.

## Features

✅ **Koha Integration** - Direct API connection to Koha ILS  
✅ **RFID Support** - Read/write RFID tags at gates  
✅ **Central System Login** - Authentication to central library server  
✅ **Book Checkout/Checkin** - Complete circulation workflows  
✅ **Real-time Sync** - Keeps data in sync with central system  
✅ **Offline Support** - Works when disconnected from central server  
✅ **Configurable** - Easy setup for new systems/IPs  

## Architecture

```
LibraryApp (Windows/Cross-platform)
├── UI Layer (WPF or Web-based)
├── API Client (Koha + Central System)
├── RFID Driver
├── Local SQLite Cache
└── Configuration Manager
```

## Quick Start

### Prerequisites
- .NET 6.0+ (for cross-platform) or .NET Framework 4.8+
- Koha 19.05+
- Central Library System running
- RFID Reader (MR6100 or compatible)

### Setup

1. **Clone the repo:**
   ```bash
   git clone https://github.com/cg4151/LibraryApp-Modern.git
   cd LibraryApp-Modern
   ```

2. **Configure settings:**
   ```bash
   cp appsettings.example.json appsettings.json
   ```
   
   Edit `appsettings.json`:
   ```json
   {
     "CentralSystem": {
       "BaseUrl": "https://YOUR_CENTRAL_IP:PORT/nhrmbase",
       "Username": "admin",
       "Password": "password"
     },
     "Koha": {
       "BaseUrl": "http://YOUR_KOHA_IP:8081",
       "ApiKey": "YOUR_API_KEY"
     },
     "RFID": {
       "ReaderIp": "192.168.1.100",
       "ReaderPort": 10001
     }
   }
   ```

3. **Run the app:**
   ```bash
   dotnet run
   ```

## Directory Structure

```
LibraryApp-Modern/
├── src/
│   ├── LibraryApp.Core/          Core business logic
│   ├── LibraryApp.Infrastructure/  Database & API clients
│   ├── LibraryApp.UI/              WPF Desktop App
│   └── LibraryApp.Web/             Web interface (optional)
├── tests/
│   ├── LibraryApp.Core.Tests/
│   └── LibraryApp.Infrastructure.Tests/
├── appsettings.example.json
├── docker-compose.yml             (for local dev)
└── README.md
```

## Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "CentralSystem": {
    "BaseUrl": "https://10.10.10.24/nhrmbase",
    "Username": "librarian",
    "Password": "secure_password",
    "Timeout": 30
  },
  "Koha": {
    "BaseUrl": "http://koha-server:8081",
    "ApiKey": "your-api-key-here",
    "Timeout": 15
  },
  "RFID": {
    "Enabled": true,
    "ReaderIp": "192.168.1.100",
    "ReaderPort": 10001,
    "ReadTimeout": 5
  },
  "Database": {
    "Type": "SQLite",
    "ConnectionString": "Data Source=library.db"
  },
  "Cache": {
    "Enabled": true,
    "SyncIntervalMinutes": 5
  }
}
```

## Key Workflows

### 1. Book Checkout
```
User Login → Scan Book RFID → Verify in Koha → Update Central System → Confirm
```

### 2. Book Checkin
```
Scan Return RFID → Verify Book Status → Update Koha → Sync Central → Done
```

### 3. Gate RFID Writing
```
Scan New Book → Write RFID Tag → Verify Write → Koha Update → Complete
```

## API Endpoints (if using Web)

```
GET    /api/books/{id}              Get book details
POST   /api/checkout                Checkout a book
POST   /api/checkin                 Checkin a book
GET    /api/rfid/status             RFID reader status
POST   /api/rfid/write              Write RFID tag
POST   /api/auth/login              Login to system
```

## Troubleshooting

### Central System Connection Failed
- ✓ Check IP address in `appsettings.json`
- ✓ Verify HTTPS certificate
- ✓ Check firewall rules
- ✓ Review logs in `logs/`

### RFID Reader Not Found
- ✓ Check reader IP/port in config
- ✓ Verify reader is powered on
- ✓ Test connection: `ping READER_IP`

### Koha Sync Issues
- ✓ Verify API key is correct
- ✓ Check Koha server is running
- ✓ Review API logs on Koha

## Logs

Logs are written to `logs/` directory with daily rotation:
```
logs/
├── 2024-08-31.log
├── 2024-09-01.log
└── ...
```

## Development

### Run Tests
```bash
dotnet test
```

### Build Release
```bash
dotnet publish -c Release
```

## License

MIT

## Support

For issues, create an issue on GitHub or contact support.
