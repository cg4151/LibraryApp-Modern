# Quick Start Guide - LibraryApp-Modern Integration

## 🚀 5-Minute Setup

### **Step 1: Clone & Configure**
```bash
git clone https://github.com/cg4151/LibraryApp-Modern.git
cd LibraryApp-Modern

# Copy configuration template
cp appsettings.enhanced.json appsettings.json
```

### **Step 2: Set Environment Variables**
```bash
# Linux/Mac
export CENTRAL_SYSTEM_USER="librarian"
export CENTRAL_SYSTEM_PASSWORD="your_secure_password"
export KOHA_API_KEY="your_koha_api_key"
export ENCRYPTION_KEY="your_encryption_key"

# Windows (PowerShell)
$env:CENTRAL_SYSTEM_USER="librarian"
$env:CENTRAL_SYSTEM_PASSWORD="your_secure_password"
$env:KOHA_API_KEY="your_koha_api_key"
$env:ENCRYPTION_KEY="your_encryption_key"
```

### **Step 3: Update IP Addresses**
Edit `appsettings.json` with your network configuration:
```json
{
  "CentralSystem": {
    "BaseUrl": "https://YOUR_CENTRAL_SYSTEM_IP:PORT/nhrmbase"
  },
  "Koha": {
    "BaseUrl": "http://YOUR_KOHA_IP:8081"
  },
  "RFID": {
    "ConnectionModes": {
      "TCP": {
        "ReaderIp": "YOUR_RFID_READER_IP",
        "ReaderPort": 10001
      },
      "SerialCOM": {
        "ComPort": "COM1"
      }
    }
  }
}
```

### **Step 4: Run Application**
```bash
dotnet run
```

---

## 📖 Essential Files to Review (in order)

1. **README.md** - Project overview
2. **INTEGRATION_GUIDE.md** - Strategic recommendations ⭐
3. **appsettings.enhanced.json** - Configuration reference
4. **IMPLEMENTATION_COMPLETE.md** - Full technical details
5. Source files:
   - `src/LibraryApp.Core/Services/RfidAuditService.cs`
   - `src/LibraryApp.Core/Services/GateRfidWriteService.cs`
   - `src/LibraryApp.Infrastructure/Caching/LocalCacheService.cs`
   - `src/LibraryApp.Infrastructure/CentralSystem/CentralSystemClient.cs`

---

## 🎯 Core Features Ready to Use

### **1. RFID Gate Write Operations**
```csharp
var result = await gateRfidWriteService.WriteBookAtGateAsync(
    accessionNo: "ACC-001234",
    bookTitle: "Modern C#",
    isbn: "978-1234567890",
    gateId: "GATE-01",
    operatorId: "LIB001"
);
```

**Result includes**:
- ✅ Write success/failure status
- ✅ RFID UID generated
- ✅ Koha update status
- ✅ Central System sync status
- ✅ Audit trail logged

### **2. Offline Operations Caching**
```csharp
// Queue checkout for offline processing
await localCacheService.QueueCheckoutAsync("ACC-001234", "PATRON-999");

// Later, when connection restored, automatic sync happens
var syncStatus = await localCacheService.SyncCachedChangesAsync();
```

### **3. Audit Trail Logging**
```csharp
// Automatically logged to:
// logs/audit/rfid-write-2026-09-03.csv
// logs/audit/circulation-2026-09-03.csv
// logs/audit/gate-operations-2026-09-03.csv
```

### **4. Dual RFID Reader Support**
```csharp
// Automatically tries TCP first, falls back to Serial COM
var reader = await rfidReaderFactory.CreateReaderWithFailover();
```

---

## 🧪 Testing the Integration

### **Test 1: Verify RFID Reader Connection**
```bash
# Check TCP connection
ping YOUR_RFID_READER_IP

# Verify port is open
telnet YOUR_RFID_READER_IP 10001
```

### **Test 2: Verify Koha Connection**
```bash
curl -X GET http://YOUR_KOHA_IP:8081/api/v1/items \
  -H "Authorization: Bearer YOUR_KOHA_API_KEY"
```

### **Test 3: Verify Central System Connection**
```bash
curl -X GET https://YOUR_CENTRAL_SYSTEM_IP:PORT/nhrmbase/health
```

### **Test 4: Test Gate Write Operation**
```bash
curl -X POST http://localhost:5000/api/gate/write-tag \
  -H "Content-Type: application/json" \
  -d '{
    "accessionNo": "TEST-001",
    "bookTitle": "Test Book",
    "isbn": "978-0000000000",
    "gateId": "GATE-01"
  }'
```

---

## 📊 Directory Structure

```
LibraryApp-Modern/
├── README.md                           # Project overview
├── SETUP.md                            # Original setup guide
├── INTEGRATION_GUIDE.md                # 📍 Strategic roadmap
├── IMPLEMENTATION_COMPLETE.md          # 📍 Technical details
├── QUICK_START.md                      # 📍 This file
├── appsettings.example.json            # Original example
├── appsettings.enhanced.json           # 📍 Enhanced configuration
│
├── src/
│   ├── LibraryApp.Core/
│   │   ├── Services/
│   │   │   ├── RfidAuditService.cs              # 📍 NEW
│   │   │   ├── GateRfidWriteService.cs          # 📍 NEW
│   │   │   ├── RfidReaderFactory.cs             # 📍 NEW
│   │   │   └── (existing services...)
│   │   │
│   │   └── Models/
│   │       ├── BookRfidAudit.cs                 # 📍 NEW
│   │       ├── CirculationAudit.cs              # 📍 NEW
│   │       └── (existing models...)
│   │
│   ├── LibraryApp.Infrastructure/
│   │   ├── Caching/
│   │   │   └── LocalCacheService.cs             # 📍 NEW
│   │   │
│   │   ├── CentralSystem/
│   │   │   └── CentralSystemClient.cs           # 📍 NEW
│   │   │
│   │   └── (existing infrastructure...)
│   │
│   └── LibraryApp.UI/
│       └── (WPF or Web UI components)
│
├── tests/
│   ├── LibraryApp.Core.Tests/
│   └── LibraryApp.Infrastructure.Tests/
│
├── logs/                               # Automatically created
│   ├── system/
│   ├── rfid/
│   ├── koha/
│   ├── central-system/
│   └── audit/
│
└── docker-compose.yml                  # For local development
```

📍 = NEW or ENHANCED files

---

## 🔧 Dependency Injection Setup

Add this to your `Startup.cs` or `Program.cs`:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    var configuration = Configuration;
    
    // Load configurations
    var rfidConfig = configuration.GetSection("RFID").Get<RfidConfiguration>();
    var centralSystemConfig = configuration.GetSection("CentralSystem").Get<CentralSystemConfiguration>();
    
    // Register RFID services
    services.AddSingleton(rfidConfig);
    services.AddScoped<IRfidReaderFactory, RfidReaderFactory>();
    services.AddScoped<IGateRfidWriteService, GateRfidWriteService>();
    
    // Register audit service
    var auditPath = configuration["LogPaths:AuditTrail"];
    services.AddScoped<IRfidAuditService>(sp => 
        new RfidAuditService(auditPath, sp.GetRequiredService<ILogger<RfidAuditService>>())
    );
    
    // Register local cache
    services.AddDbContext<LocalCacheDbContext>(options =>
        options.UseSqlite(configuration.GetConnectionString("Cache"))
    );
    services.AddScoped<ILocalCacheService, LocalCacheService>();
    
    // Register Central System client
    services.AddSingleton(centralSystemConfig);
    services.AddHttpClient<ICentralSystemService, CentralSystemClient>();
    
    // Add other services...
}
```

---

## 🚨 Troubleshooting

### Issue: RFID Reader Not Connecting
```
Check:
1. appsettings.json has correct ReaderIp and ReaderPort
2. Reader is powered on
3. Network cable is connected
4. Firewall allows port 10001
5. Check logs/rfid/[date].log for detailed errors
→ Will automatically failover to Serial COM if configured
```

### Issue: Koha API Returns 401
```
Check:
1. KOHA_API_KEY environment variable is set
2. API key is valid in Koha
3. Koha server is running
4. Check logs/koha/[date].log
```

### Issue: Central System Connection Refused
```
Check:
1. BaseUrl is correct in appsettings.json
2. Central System service is running
3. HTTPS certificate is valid (or set TrustSelfSigned: true for testing)
4. Credentials are correct
5. Check logs/central-system/[date].log
```

### Issue: RFID Write Returns WRITE_FAILED
```
Check:
1. RFID reader has power and network connection
2. RFID tag is blank/writable
3. Tag supports content size
4. WriteRetries is set appropriately (default: 2)
5. Check logs/rfid/[date].log for specific errors
```

---

## 📈 Monitoring

### View Audit Trail
```bash
# List recent gate operations
tail -f logs/audit/gate-operations-$(date +%Y-%m-%d).csv

# Count successful writes
grep SUCCESS logs/audit/gate-operations-$(date +%Y-%m-%d).csv | wc -l

# Check failures
grep FAILED logs/audit/gate-operations-$(date +%Y-%m-%d).csv
```

### Check System Health
```bash
# View system logs
tail -f logs/system/$(date +%Y-%m-%d).log

# Check RFID operations
tail -f logs/rfid/$(date +%Y-%m-%d).log

# Monitor Koha sync
tail -f logs/koha/$(date +%Y-%m-%d).log

# Check Central System sync
tail -f logs/central-system/$(date +%Y-%m-%d).log
```

---

## 📚 Next Steps

1. **Review Documentation**
   - Read `INTEGRATION_GUIDE.md` for strategic decisions
   - Review `IMPLEMENTATION_COMPLETE.md` for technical details

2. **Implement Missing Pieces**
   - Complete `IKohaService` implementation in Infrastructure layer
   - Create REST API controllers for Gate operations
   - Build UI for RFID configuration

3. **Run Tests**
   ```bash
   dotnet test
   ```

4. **Deploy**
   - Use `docker-compose.yml` for local development
   - Build release: `dotnet publish -c Release`
   - Deploy to staging for testing

---

## ✅ Pre-Deployment Checklist

- [ ] All IP addresses configured correctly
- [ ] Environment variables set securely
- [ ] Koha API key validated
- [ ] Central System credentials verified
- [ ] RFID reader tested (TCP and/or COM)
- [ ] SSL/HTTPS certificates installed
- [ ] Database migrations applied
- [ ] Audit log directory has write permissions
- [ ] Application runs without errors: `dotnet run`
- [ ] Health check endpoint responds: `GET /api/health/status`

---

## 📞 Support Resources

**For RFID Issues**: Check `INTEGRATION_GUIDE.md` → Troubleshooting → RFID Reader Not Found

**For Koha Issues**: Check `INTEGRATION_GUIDE.md` → Troubleshooting → Koha Sync Issues

**For Central System Issues**: Check `INTEGRATION_GUIDE.md` → Troubleshooting → Central System Connection Failed

**For Logging Issues**: Check `logs/error/[date].log`

---

## 🎯 Key Takeaways

✅ **Dual RFID Support**: TCP (primary) + Serial COM (fallback)  
✅ **Offline Capable**: Full SQLite cache for operations when disconnected  
✅ **Audit Ready**: Every operation logged to CSV with operator ID  
✅ **Legacy Compatible**: Central System multi-endpoint integration  
✅ **Professional**: Modern .NET 6.0+, SOLID principles, dependency injection  
✅ **Secure**: Environment-based credentials, encrypted data support  

---

**Ready to go!** 🚀

For detailed information, see the complete documents in the repository.
