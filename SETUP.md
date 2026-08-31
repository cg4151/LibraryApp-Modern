# Setup Guide for NSBM Library App

## Prerequisites

1. **Windows 10/11** or **Linux/Mac** with .NET 6.0+
2. **Koha ILS** (19.05 or later)
3. **RFID Reader** (MR6100 or compatible)
4. **MySQL/MariaDB** database
5. **Network connectivity** between all components

## Step 1: Configuration

### Copy Configuration Template
```bash
cp appsettings.example.json appsettings.json
```

### Edit appsettings.json
```json
{
  "CentralSystem": {
    "BaseUrl": "https://YOUR_NEW_IP:PORT/nhrmbase",
    "Username": "librarian",
    "Password": "your_password"
  },
  "Koha": {
    "BaseUrl": "http://YOUR_KOHA_SERVER:8081",
    "ApiKey": "your-koha-api-key"
  },
  "RFID": {
    "ReaderIp": "YOUR_RFID_READER_IP",
    "ReaderPort": 10001
  }
}
```

## Step 2: Find Your System IPs

### Central System IP
```bash
# Windows
ipconfig

# Linux/Mac
ifconfig
```

Look for your central library server IP. Example: `10.10.10.100`

### RFID Reader IP
```bash
# Ping your RFID reader
ping 192.168.1.100
```

If not responding, check:
- Is the reader powered on?
- Is it on the same network?
- Check reader manual for default IP

### Koha Server
Typically at: `http://koha-server:8081` or `http://192.168.x.x:8081`

## Step 3: Get Koha API Key

1. Log into Koha as admin
2. Go to **Administration → Users and Permissions → API Keys**
3. Create a new API key for the library app
4. Copy the key to `appsettings.json`

## Step 4: Build & Run

### Using .NET CLI
```bash
dotnet build
dotnet run
```

### Using Docker
```bash
docker-compose up
```

### Using Visual Studio
1. Open `LibraryApp.sln`
2. Press F5 or click **Run**

## Step 5: Verify Connection

On startup, you should see:
```
✓ Koha Connected
✓ Central System Connected
✓ RFID Reader Connected
```

If any show ✗, check:
1. Network connectivity
2. IP addresses in config
3. Firewall rules
4. Service is running on target IP

## Troubleshooting

### Central System Connection Failed
```bash
# Test connection
ping YOUR_CENTRAL_SYSTEM_IP

# Check firewall
netsh advfirewall show allprofiles  # Windows
sudo ufw status                      # Linux
```

### RFID Reader Not Found
```bash
# Verify reader is online
ping YOUR_RFID_READER_IP

# Check reader config
# Refer to RFID reader manual for IP configuration
```

### Koha API Issues
- Verify API key in `appsettings.json`
- Check Koha server is running
- Verify REST API is enabled in Koha

## First Time Usage

1. **Login**: Enter your librarian credentials
2. **Scan Book**: Place book on RFID reader or enter book ID
3. **Checkout**: Scan member card or enter member ID
4. **Confirm**: Transaction syncs to central system

## Database Setup

The app uses SQLite by default. On first run:
```bash
dotnet run
# Migrations run automatically
```

To switch to MySQL:
```json
{
  "Database": {
    "Type": "MySQL",
    "ConnectionString": "Server=localhost;User=root;Password=password;Database=library;"
  }
}
```

## Support

For issues:
1. Check logs in `logs/` directory
2. Review this guide
3. Create an issue on GitHub
