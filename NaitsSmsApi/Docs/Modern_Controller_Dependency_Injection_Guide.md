# Modern Controller Dependency Injection Guide - .NET 9

## ?? **Overview**

This guide explains how to convert legacy controller constructors using `ServiceLocator` to modern dependency injection patterns in .NET 9.

---

## ?? **Legacy vs Modern Constructor Comparison**

### **Legacy Pattern (.NET Framework)**

```csharp
// RegistryLegacyController.cs (OLD)
public class RegistryLegacyController : Controller
{
    private readonly IRegistrationServise _registrationService;

    public RegistryLegacyController()
    {
        // ? SERVICE LOCATOR ANTI-PATTERN
        _registrationService = ServiceLocator.Get<IRegistrationServise>();
    }
}
```

**Problems with Legacy Approach:**
- ? Service Locator is an anti-pattern
- ? Hidden dependencies (hard to test)
- ? No compile-time safety
- ? Poor testability
- ? Tight coupling to infrastructure

---

### **Modern Pattern (.NET 9)**

```csharp
// RegistryController.cs (NEW)
[ApiController]
[Route("api/[controller]")]
public class RegistryController : ControllerBase
{
    private readonly IRegistrationService _registrationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RegistryController> _logger;

    // ? CONSTRUCTOR INJECTION PATTERN
    public RegistryController(
        IRegistrationService registrationService,
        IUnitOfWork unitOfWork,
        ILogger<RegistryController> logger)
    {
        _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
```

**Benefits of Modern Approach:**
- ? Explicit dependencies (visible in constructor)
- ? Easy to test (inject mocks)
- ? Compile-time safety
- ? Loose coupling
- ? Follows SOLID principles

---

## ?? **Step-by-Step Migration Guide**

### **Step 1: Identify Dependencies**

**Legacy Controller Analysis:**
```csharp
// OLD: Hidden dependencies
_registrationService = ServiceLocator.Get<IRegistrationServise>();
_otherService = ServiceLocator.Get<IOtherService>();
```

**List of Dependencies:**
1. `IRegistrationService` - Business logic
2. `IUnitOfWork` - Transaction management
3. `ILogger<T>` - Logging (recommended)
4. Any other services used in the controller

---

### **Step 2: Define Constructor Parameters**

```csharp
public RegistryController(
    IRegistrationService registrationService,  // Required
    IUnitOfWork unitOfWork,                    // Required for SaveChanges
    ILogger<RegistryController> logger)        // Recommended for diagnostics
{
    _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
    _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

**Null Checks:**
- ? Always validate injected dependencies
- ? Throw `ArgumentNullException` if null
- ? Prevents runtime errors

---

### **Step 3: Register Services in Program.cs**

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register services for dependency injection
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// DbContext (if not already registered)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add controllers
builder.Services.AddControllers();

var app = builder.Build();
```

**Service Lifetimes:**
- `AddScoped`: Created once per request (recommended for services with DbContext)
- `AddSingleton`: Created once for application lifetime
- `AddTransient`: Created every time requested

---

## ?? **Common Patterns**

### **Pattern 1: Simple CRUD Operation**

```csharp
[HttpGet("classes")]
public async Task<IActionResult> GetClasses(CancellationToken ct)
{
    try
    {
        // Service method handles everything
        var classes = await _registrationService.GetClass(ct);
        return Ok(classes);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving classes");
        return StatusCode(500, new { message = "An error occurred" });
    }
}
```

**When to use:**
- ? Simple read operations
- ? Service handles all logic
- ? No explicit transaction needed

---

### **Pattern 2: Create with Automatic Commit**

```csharp
[HttpPost("classes")]
public async Task<IActionResult> CreateClass([FromBody] ClassDto model, CancellationToken ct)
{
    try
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Service method includes SaveChanges internally
        var result = await _registrationService.CreateClass(model, ct);
        
        if (result)
            return Ok(new { message = "Class created successfully" });

        return BadRequest(new { message = "Failed to create class" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating class");
        return StatusCode(500, new { message = "An error occurred" });
    }
}
```

**When to use:**
- ? Service method calls `SaveChanges` internally
- ? Single operation
- ? No complex transaction logic

---

### **Pattern 3: Create with Explicit Commit**

```csharp
[HttpPost("students")]
public async Task<IActionResult> CreateStudent([FromBody] StudentDetailsDto model, CancellationToken ct)
{
    try
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Service method does NOT commit
        var result = await _registrationService.CreatStudent(model, ct);

        if (result == 0)
        {
            // ? EXPLICIT COMMIT IN CONTROLLER
            await _unitOfWork.SaveChangesAsync(ct);
            return Ok(new { message = "Student created successfully" });
        }

        return BadRequest(new { message = "Failed to create student" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating student");
        // ? NO SaveChanges called = automatic rollback
        return StatusCode(500, new { message = "An error occurred" });
    }
}
```

**When to use:**
- ? Need fine-grained control over transactions
- ? Multiple operations that should commit together
- ? Service method doesn't commit

---

### **Pattern 4: Complex Transaction with Multiple Operations**

```csharp
[HttpPost("complex-operation")]
public async Task<IActionResult> ComplexOperation([FromBody] ComplexDto model, CancellationToken ct)
{
    try
    {
        _logger.LogInformation("Starting complex operation");

        // Operation 1
        var result1 = await _registrationService.CreateClass(model.ClassDto, ct);
        if (!result1)
            return BadRequest(new { message = "Failed at step 1" });

        // Operation 2
        var result2 = await _registrationService.CreatClassArm(model.ClassArmDto, ct);
        if (!result2)
        {
            // Don't commit - transaction rolls back
            return BadRequest(new { message = "Failed at step 2" });
        }

        // Operation 3
        var result3 = await _registrationService.CreatClassSubject(model.SubjectDto, model.SubjectStatus, ct);
        if (!result3)
            return BadRequest(new { message = "Failed at step 3" });

        // ? ALL SUCCESSFUL - COMMIT ONCE
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Complex operation completed successfully");
        return Ok(new { message = "All operations completed successfully" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during complex operation");
        // ? Automatic rollback - no commit
        return StatusCode(500, new { message = "An error occurred" });
    }
}
```

**When to use:**
- ? Multiple related operations
- ? All-or-nothing transaction semantics
- ? Need to rollback if any operation fails

---

### **Pattern 5: Long-Running Operation with Progress**

```csharp
[HttpPost("students/automatic-promotion")]
public async Task<IActionResult> StudentsAutomaticPromotion(CancellationToken ct)
{
    try
    {
        _logger.LogInformation("Starting school-wide promotion");

        // Long-running operation
        // This method handles its own SaveChanges internally
        await _registrationService.StudentsAutomaticPromotion(ct);

        _logger.LogInformation("Promotion completed successfully");
        return Ok(new { 
            message = "School-wide promotion completed",
            details = "All students promoted, new session created"
        });
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning("Promotion was cancelled");
        return StatusCode(499, new { message = "Operation cancelled" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during promotion");
        return StatusCode(500, new { message = "An error occurred" });
    }
}
```

**When to use:**
- ? Long-running operations
- ? Service handles transactions internally
- ? Need cancellation support

---

## ?? **Decision Tree: When to Call SaveChanges**

```
???????????????????????????????????????????????????
?  Does service method call SaveChanges?         ?
????????????????????????????????????????????????????
             ?                    ?
        ???????????          ???????????
        ?   YES   ?          ?   NO    ?
        ???????????          ???????????
             ?                    ?
    ???????????????????   ??????????????????
    ? Don't call it   ?   ? Call it in     ?
    ? in controller   ?   ? controller     ?
    ???????????????????   ??????????????????
```

### **Service Calls SaveChanges Internally:**
```csharp
// Service method
public async Task<bool> CreateClass(ClassDto model, CancellationToken ct)
{
    // ... business logic ...
    await _uow.SaveChangesAsync(ct);  // ? Service commits
    return true;
}

// Controller - No SaveChanges needed
[HttpPost("classes")]
public async Task<IActionResult> CreateClass([FromBody] ClassDto model, CancellationToken ct)
{
    var result = await _registrationService.CreateClass(model, ct);
    return result ? Ok() : BadRequest();
}
```

### **Service Does NOT Call SaveChanges:**
```csharp
// Service method
public async Task<byte> CreatStudent(StudentDetailsDto model, CancellationToken ct)
{
    // ... business logic ...
    // ? NO SaveChanges here
    return 0;
}

// Controller - MUST call SaveChanges
[HttpPost("students")]
public async Task<IActionResult> CreateStudent([FromBody] StudentDetailsDto model, CancellationToken ct)
{
    var result = await _registrationService.CreatStudent(model, ct);
    await _unitOfWork.SaveChangesAsync(ct);  // ? Controller commits
    return Ok();
}
```

---

## ?? **Transaction Management Best Practices**

### **1. Single Responsibility**
```csharp
// ? GOOD - Controller handles HTTP, Service handles business logic
[HttpPost("classes")]
public async Task<IActionResult> CreateClass([FromBody] ClassDto model, CancellationToken ct)
{
    var result = await _registrationService.CreateClass(model, ct);
    return result ? Ok() : BadRequest();
}

// ? BAD - Controller has business logic
[HttpPost("classes")]
public async Task<IActionResult> CreateClass([FromBody] ClassDto model, CancellationToken ct)
{
    // Don't do business logic in controller!
    if (string.IsNullOrEmpty(model.ClassName))
        return BadRequest();
    
    var repository = _unitOfWork.Repository<RxClasss>();
    // ... more business logic ...
}
```

### **2. Consistent Error Handling**
```csharp
[HttpPost("endpoint")]
public async Task<IActionResult> Endpoint([FromBody] Dto model, CancellationToken ct)
{
    try
    {
        // Validation
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Business logic
        var result = await _service.Method(model, ct);

        // Success
        if (result)
        {
            await _unitOfWork.SaveChangesAsync(ct);  // Commit if needed
            return Ok(new { message = "Success" });
        }

        // Failure
        return BadRequest(new { message = "Failed" });
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning("Operation cancelled");
        return StatusCode(499, new { message = "Cancelled" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in endpoint");
        return StatusCode(500, new { message = "Internal error" });
    }
}
```

### **3. Automatic Rollback**
```csharp
[HttpPost("endpoint")]
public async Task<IActionResult> Endpoint([FromBody] Dto model, CancellationToken ct)
{
    try
    {
        // Multiple operations
        await _service.Operation1(ct);
        await _service.Operation2(ct);
        
        // Something goes wrong here
        throw new Exception("Oops!");
        
        // This SaveChanges never executes
        await _unitOfWork.SaveChangesAsync(ct);
    }
    catch (Exception ex)
    {
        // ? Automatic rollback - no data persisted
        return StatusCode(500);
    }
}
```

---

## ?? **Common Dependency Injection Scenarios**

### **Scenario 1: Multiple Services**
```csharp
public class RegistryController : ControllerBase
{
    private readonly IRegistrationService _registrationService;
    private readonly IAssessmentService _assessmentService;
    private readonly IPaymentService _paymentService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RegistryController> _logger;

    public RegistryController(
        IRegistrationService registrationService,
        IAssessmentService assessmentService,
        IPaymentService paymentService,
        IUnitOfWork unitOfWork,
        ILogger<RegistryController> logger)
    {
        _registrationService = registrationService;
        _assessmentService = assessmentService;
        _paymentService = paymentService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }
}
```

### **Scenario 2: Optional Dependencies**
```csharp
public class RegistryController : ControllerBase
{
    private readonly IRegistrationService _registrationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RegistryController> _logger;
    private readonly IEmailService? _emailService;  // Optional

    public RegistryController(
        IRegistrationService registrationService,
        IUnitOfWork unitOfWork,
        ILogger<RegistryController> logger,
        IEmailService? emailService = null)  // Optional parameter
    {
        _registrationService = registrationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _emailService = emailService;
    }

    [HttpPost("notify")]
    public async Task<IActionResult> SendNotification(CancellationToken ct)
    {
        // Check if optional service is available
        if (_emailService != null)
        {
            await _emailService.SendEmail(ct);
        }
        else
        {
            _logger.LogWarning("Email service not available");
        }
        
        return Ok();
    }
}
```

### **Scenario 3: Configuration Injection**
```csharp
public class RegistryController : ControllerBase
{
    private readonly IRegistrationService _registrationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RegistryController> _logger;
    private readonly IConfiguration _configuration;

    public RegistryController(
        IRegistrationService registrationService,
        IUnitOfWork unitOfWork,
        ILogger<RegistryController> logger,
        IConfiguration configuration)
    {
        _registrationService = registrationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("settings")]
    public IActionResult GetSettings()
    {
        var maxStudents = _configuration.GetValue<int>("MaxStudentsPerClass");
        return Ok(new { maxStudents });
    }
}
```

---

## ? **Migration Checklist**

### **For Each Legacy Controller:**

- [ ] **1. Identify all ServiceLocator.Get<T>() calls**
  ```csharp
  // Find all these in legacy code
  _service1 = ServiceLocator.Get<IService1>();
  _service2 = ServiceLocator.Get<IService2>();
  ```

- [ ] **2. Add constructor parameters**
  ```csharp
  public ModernController(
      IService1 service1,
      IService2 service2,
      IUnitOfWork unitOfWork,
      ILogger<ModernController> logger)
  ```

- [ ] **3. Add null checks**
  ```csharp
  _service1 = service1 ?? throw new ArgumentNullException(nameof(service1));
  ```

- [ ] **4. Register in Program.cs**
  ```csharp
  builder.Services.AddScoped<IService1, Service1>();
  builder.Services.AddScoped<IService2, Service2>();
  ```

- [ ] **5. Test dependency injection**
  ```csharp
  // Run application - DI container will inject dependencies
  ```

- [ ] **6. Identify SaveChanges locations**
  - Does service call SaveChanges?
  - Should controller call SaveChanges?

- [ ] **7. Add transaction management**
  ```csharp
  await _unitOfWork.SaveChangesAsync(ct);
  ```

- [ ] **8. Add error handling**
  ```csharp
  try { ... } catch { /* rollback happens automatically */ }
  ```

- [ ] **9. Add logging**
  ```csharp
  _logger.LogInformation("Operation started");
  _logger.LogError(ex, "Operation failed");
  ```

- [ ] **10. Test endpoints**
  - Successful operations
  - Failed operations (rollback)
  - Cancellation
  - Error scenarios

---

## ?? **Summary**

### **Key Differences**

| **Aspect** | **Legacy** | **Modern** |
|-----------|-----------|-----------|
| **Dependency Resolution** | `ServiceLocator.Get<T>()` | Constructor injection |
| **Visibility** | Hidden | Explicit in constructor |
| **Testability** | Difficult | Easy (mock injection) |
| **Compile-time Safety** | ? No | ? Yes |
| **Transaction Management** | Manual (`UnitOfWork.Commit()`) | Async (`await _uow.SaveChangesAsync(ct)`) |
| **Error Handling** | Try-catch | Try-catch + automatic rollback |
| **Logging** | Manual | Injected `ILogger<T>` |

### **Modern Pattern Benefits**

? **Explicit Dependencies:** All dependencies visible in constructor  
? **Easy Testing:** Inject mocks in unit tests  
? **Compile-time Safety:** Errors caught at compile time  
? **Automatic Rollback:** Transactions rollback on exception  
? **Better Logging:** Structured logging with `ILogger<T>`  
? **Follows SOLID:** Single Responsibility, Dependency Inversion  

---

## ?? **You're Ready!**

You now know how to:
1. ? Replace `ServiceLocator` with constructor injection
2. ? Inject `IRegistrationService`, `IUnitOfWork`, `ILogger<T>`
3. ? Call `SaveChangesAsync(ct)` in controllers when needed
4. ? Handle transactions correctly
5. ? Test with mocked dependencies

**Your modern .NET 9 controllers are production-ready!** ??
