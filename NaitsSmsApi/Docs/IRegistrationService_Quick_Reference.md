# IRegistrationService Interface - Quick Reference Guide

## ?? **Quick Access Table of Contents**

Jump to any section by searching for the method group number.

---

## ?? **Method Quick Finder**

### **By Functionality**

| **I need to...** | **Method(s)** | **Group** |
|------------------|---------------|-----------|
| **Get all classes** | `GetClass()` | GROUP 1 |
| **Create a class** | `CreateClass()` | GROUP 1 |
| **Create a student** | `CreatStudent()` (2 overloads) | GROUP 1 |
| **Get students in an arm** | `GetStudentForAnArm()` | GROUP 1 |
| **Validate subjects** | `DoesThisXSubjectAlreadyExist()` | GROUP 1-2 |
| **Search teachers** | `GetTeacherByUserName()` | GROUP 2 |
| **Create school registrar** | `CreateSchoolRegistrar()` | GROUP 3 |
| **Assign teacher to subject** | `AssignTeacherSubjectArm()` | GROUP 4 |
| **Get subject lists** | `SeniorClassesSubjectList()`, `BasicClassesSubjectList()` | GROUP 4 |
| **Get school info** | `GetSchoolInfo()`, `GetSchoolInfoAndProprietorCode()` | GROUP 5 |
| **Create school head** | `CreatSchoolHead()` | GROUP 6 |
| **Update teacher** | `UpdateTeacherInfo()` | GROUP 6 |
| **Delete student** | `DeleteStudent()` | GROUP 6 |
| **Update student details** | `UpdateStudentDetails()` | GROUP 7 |
| **Manage academic sessions** | `CreateAcademicSession()`, `GetAcademicSessions()` | GROUP 7-8 |
| **Prepare assessments** | `PrepareAssessmentEnvironmentForTheWholeStudents()` | GROUP 8 |
| **Promote students** | `PromoteStudents()`, `AutoPromotion()`, `StudentsAutomaticPromotion()` | GROUP 8, 17 |
| **Undo promotion** | `UndoRecentStudentsPromotion()` (3 versions) | GROUP 9 |
| **Search students** | `GetStudentByAdmissionNo()`, `GetStudentsByParameter()` | GROUP 10, 16 |
| **Manage users** | `EnableUserById()`, `DisableUserById()` | GROUP 10 |
| **Get attendance** | `GetCurrentTotalAttendance()`, `CreateTotalAttendance()` | GROUP 11 |
| **Transfer teacher assignments** | `TransferSubjectTeacherSubjectAssignment()` | GROUP 12 |
| **Manage electives** | `ChangeStudentsElectiveSelectionStatus()` | GROUP 12-13 |
| **Change password** | `ChangeStudentPassword()` | GROUP 13 |
| **Set pass mark** | `SubmitPassMark()`, `GetPassMark()` | GROUP 13-14 |
| **Process payments** | `PayForSelectedReturningStudents()`, `PayForSelectedFreshiersRegistration()` | GROUP 14 |
| **Get payment history** | `GetSelectedStudentPaymentHistoryById()` | GROUP 14, 16 |
| **Get school fees** | `GetClassLevelSchoolFees()`, `AddOrUpdateFeePrices()` | GROUP 14 |
| **Get school summary** | `GetSchoolSummaryForRegistrarDashboard()` | GROUP 14 |
| **Generate dropdowns** | `ClassArmDropdown()`, `GenderDropdown()`, etc. | All Groups |
| **Validate subscription** | `ValidateSchoolSubscriptionStatus()` | GROUP 15 |
| **Payment reports** | `GetTermlyReportByTermId()` | GROUP 16 |
| **Distribute students** | `DistributeStudentEvenly()` | GROUP 16 |
| **Manage SMS** | `GetSmsBalance()`, `SubmitSmsPurchage()` | GROUP 17 |
| **Manage licensing** | `GetSubscriptionHistory()`, `GetExpiryDate()` | GROUP 17 |
| **Encode/decode credentials** | `EncodeCredentials()`, `DecodeCredentials()` | GROUP 17 |

---

## ?? **Common Usage Patterns**

### **Pattern 1: CRUD Operations**
```csharp
// CREATE
await _service.CreateClass(new ClassDto { ... }, ct);

// READ
var classes = await _service.GetClass(ct);
var classById = await _service.GetStudentById("id", ct);

// UPDATE
await _service.UpdateClass(classDto, ct);

// DELETE
await _service.DeleteClass("id", ct);
```

### **Pattern 2: Search Operations**
```csharp
// Search students by admission number
var students = await _service.GetStudentByAdmissionNo("ADM001", ct);

// Search by parameter
var results = await _service.GetStudentsByParameter("john", ct);

// Search teachers
var teachers = await _service.GetTeacherByUserName("smith", ct);
```

### **Pattern 3: Payment Processing**
```csharp
// Get students eligible for payment
var returningStudents = await _service.GetReturningStudentsList(ct);
var freshiers = await _service.GetFreshiersList(ct);

// Process payment
var receipt = await _service.PayForSelectedReturningStudents(studentIds, ct);

// Get payment history
var history = await _service.GetSelectedStudentPaymentHistoryById("studentId", ct);
```

### **Pattern 4: Assessment Preparation**
```csharp
// Prepare for whole school
var result = await _service.PrepareAssessmentEnvironmentForTheWholeStudents(status, ct);

// Prepare for specific arm
var armResult = await _service.PrepareAssessmentEnvironmentForAnArmStudents("params", ct);

// Prepare for class
var classResult = await _service.PrepareAssessmentEnvironmentForaClassStudents("params", ct);
```

### **Pattern 5: Student Promotion**
```csharp
// Manual promotion
var promotionResult = await _service.PromoteStudents(studentIds, targetArmId, ct);

// Auto promotion (JSS3 ? SS1)
await _service.AutoPromotion(ct);

// Comprehensive promotion (whole school + session management)
await _service.StudentsAutomaticPromotion(ct);

// Undo if needed
await _service.UndoRecentStudentsPromotion(ct);
```

### **Pattern 6: Dropdown Generation**
```csharp
// Generate dropdowns for UI
var classDropdown = await _service.ClassDropDown(ct);
var armDropdown = _service.ClassArmDropdown(ArmName.A);
var genderDropdown = _service.GenderDropdown(Gender.Male);
var statusDropdown = _service.StudentStatusDropdown(StudentStatus.Fresher);
```

---

## ?? **Method Count by Category**

| **Category** | **Methods** | **Percentage** |
|-------------|-----------|----------------|
| **Student Management** | ~50 | 16% |
| **Teacher Operations** | ~35 | 12% |
| **Subject/Class Mgmt** | ~40 | 13% |
| **Assessment & Promotion** | ~45 | 15% |
| **Payment & Accounting** | ~40 | 13% |
| **School Administration** | ~35 | 12% |
| **SMS & Licensing** | ~25 | 8% |
| **Utility & Helpers** | ~34 | 11% |
| **Total** | **304** | **100%** |

---

## ?? **Method Naming Conventions**

### **Prefix Patterns**
- `Get...`: Retrieve data (e.g., `GetClass`, `GetStudentById`)
- `Create...` / `Creat...`: Create new records (e.g., `CreateClass`, `CreatStudent`)
- `Update...`: Modify existing records (e.g., `UpdateClass`, `UpdateTeacherInfo`)
- `Delete...`: Remove records (e.g., `DeleteClass`, `DeleteStudent`)
- `Does...Exist`: Validation checks (e.g., `DoesThisJssSubjectAlreadyExist`)
- `Is...`: Boolean checks (e.g., `IsClassArmAlreadyExist`)
- `Prepare...`: Setup operations (e.g., `PrepareAssessmentEnvironmentForTheWholeStudents`)
- `Assign...` / `UnAssign...`: Assignment operations (e.g., `AssignTeacherSubjectArm`)

### **Suffix Patterns**
- `...Dropdown`: UI dropdown generators (e.g., `ClassArmDropdown`)
- `...Validation`: Validation methods (e.g., `PreNurserySubjectUpdateValidation`)
- `...ForAccountant`: Accounting-specific views (e.g., `GetArmStudentForAccountant`)
- `...Payment`: Payment-related operations (e.g., `PayForSelectedReturningStudents`)
- `...History`: Historical data retrieval (e.g., `GetSelectedStudentPaymentHistoryById`)

---

## ? **Performance Tips**

### **Async Best Practices**
```csharp
// ? GOOD - Uses cancellation token
await _service.GetClass(cancellationToken);

// ? GOOD - Awaits the task
var classes = await _service.GetClass(ct);

// ? BAD - Blocks the thread
var classes = _service.GetClass(ct).Result; // Don't do this!

// ? BAD - No cancellation support
await _service.GetClass(); // Missing ct parameter
```

### **Batch Operations**
```csharp
// ? GOOD - Single call for multiple students
await _service.PromoteStudents(studentIds, targetArmId, ct);

// ? BAD - Multiple calls in loop
foreach (var id in studentIds)
{
    await _service.PromoteStudents(new[] { id }, targetArmId, ct); // Slow!
}
```

---

## ??? **Error Handling**

### **Recommended Pattern**
```csharp
try
{
    var result = await _service.CreateClass(classDto, ct);
    if (result)
    {
        // Success
    }
    else
    {
        // Failed - check business logic
    }
}
catch (OperationCanceledException)
{
    // Handle cancellation
    _logger.LogInformation("Operation cancelled by user");
}
catch (Exception ex)
{
    // Handle other exceptions
    _logger.LogError(ex, "Error creating class");
    throw;
}
```

---

## ?? **Return Type Reference**

| **Return Type** | **Meaning** | **Example Methods** |
|----------------|-----------|---------------------|
| `Task<bool>` | Success/failure | `CreateClass`, `DeleteStudent` |
| `Task<byte>` | Status code | `CreatStudent`, `ValidationChecks` |
| `Task<int>` | Count or ID | `DeleteTeacher`, `BulkUpdateStudentClass` |
| `Task<string>` | Message or ID | `UpdateStudentDetails`, `ConfirmStudentsPromotion` |
| `Task<T>` | Single object | `GetStudentById`, `GetSchoolInfo` |
| `Task<IEnumerable<T>>` | Multiple objects | `GetClass`, `GetAllTeachers` |
| `Task<List<T>>` | Mutable list | `GetAllTeacher`, `GetAllStudents` |
| `Task` | Void operation | `AutoPromotion`, `DistributeStudentEvenly` |
| `IEnumerable<SelectListItem>` | Dropdown | `ClassArmDropdown`, `GenderDropdown` |

---

## ?? **Quick Start Checklist**

- [ ] Interface imported: `using NAITSSMS.Application.IService;`
- [ ] Service injected: `IRegistrationService _service`
- [ ] CancellationToken declared: `CancellationToken ct`
- [ ] Async method: `async Task<IActionResult>`
- [ ] Await all calls: `await _service.Method(ct)`
- [ ] Handle errors: `try-catch` block
- [ ] Return appropriate HTTP status
- [ ] Log operations for debugging

---

## ?? **Need Help?**

### **Documentation Files**
1. `Docs/IRegistrationService_Interface_Summary.md` - Complete reference
2. `Docs/Legacy_vs_Modern_Interface_Comparison.md` - Migration guide
3. `Docs/Interface_Update_Complete_Summary.md` - Implementation summary
4. This file - Quick reference

### **Code Files**
1. `IRegistrationService.cs` - Interface definition
2. `RegistrationService.cs` - Implementation
3. `RegistrationServiceJan2026.cs` - Legacy reference

---

## ?? **You're Ready to Build!**

With this quick reference and the complete interface, you can now:
- ? Generate API controllers
- ? Create unit tests
- ? Build integration tests
- ? Deploy to production

**Happy coding!** ??
