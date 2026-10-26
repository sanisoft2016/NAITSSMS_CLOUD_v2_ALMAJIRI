# Registry API - Endpoint Quick Reference

## ?? **Quick Access Guide**

All endpoints are prefixed with `/api/registry`

---

## ?? **Classes & Arms**

| Method | Endpoint | Description | Body |
|--------|----------|-------------|------|
| GET | `/classes` | Get all classes | - |
| POST | `/classes` | Create a class | ClassDto |
| GET | `/classes/{id}` | Get class by ID | - |
| PUT | `/classes/{id}` | Update a class | ClassDto |
| DELETE | `/classes/{id}` | Delete a class | - |
| GET | `/classes/{classId}/arms` | Get arms for a class | - |
| POST | `/classes/arms` | Create a class arm | ClassArmDto |
| PUT | `/classes/arms/{id}` | Update a class arm | ClassArmDto |
| DELETE | `/classes/arms/{id}` | Delete a class arm | - |
| GET | `/classes-and-arms` | Get all classes and arms | - |
| POST | `/classes/rearrange` | Rearrange classes | List<ClassDto> |

---

## ????? **Students**

| Method | Endpoint | Description | Body |
|--------|----------|-------------|------|
| GET | `/students` | Get all students | - |
| POST | `/students` | Create a student | StudentDetailsDto |
| GET | `/students/{id}` | Get student by ID | - |
| PUT | `/students/{id}` | Update a student | StudentDetailsDto |
| DELETE | `/students/{id}` | Delete a student | - |
| GET | `/students/recent` | Get recently saved students | - |
| GET | `/students/search` | Search students by admission no | ?admissionNo={value} |
| GET | `/students/by-parameters` | Get students by parameters | ?searchParameter={value} |
| POST | `/students/{userId}/change-password` | Change student password | StudentPasswordModelDto |
| POST | `/students/{userId}/enable` | Enable student account | - |
| POST | `/students/{userId}/disable` | Disable student account | - |
| GET | `/arms/{armId}/students` | Get students for an arm | - |
| POST | `/students/bulk-update-class` | Bulk update student class | BulkUpdateClassRequest |
| POST | `/students/distribute-evenly` | Distribute students evenly | - |

---

## ?? **Subjects**

| Method | Endpoint | Description | Body |
|--------|----------|-------------|------|
| GET | `/subjects` | Get subjects by status | ?status={SubjectStatus} |
| POST | `/subjects` | Create a subject | SubjectDto |
| GET | `/subjects/{id}` | Get subject by ID | - |
| PUT | `/subjects/{id}` | Update a subject | SubjectDto |
| DELETE | `/subjects/{id}` | Delete a subject | - |
| GET | `/subjects/{subjectName}/levels` | Get levels offering subject | - |
| GET | `/subjects/search-levels` | Search levels offering subject | ?searchParameter={value} |
| GET | `/classes/{classId}/subjects/dropdown` | Get subjects dropdown for class | - |

---

## ????? **Teachers**

| Method | Endpoint | Description | Body |
|--------|----------|-------------|------|
| GET | `/teachers` | Get all teachers | - |
| GET | `/teachers/{teacherId}` | Get teacher by ID | - |
| PUT | `/teachers/{teacherId}` | Update a teacher | UserDto |
| DELETE | `/teachers/{teacherId}` | Delete a teacher | - |
| GET | `/teachers/search` | Search teacher by username | ?userName={value} |
| GET | `/teachers/class-teachers` | Get class teachers | - |
| GET | `/teachers/subject-teachers` | Get subject teachers | - |
| GET | `/teachers/dropdown` | Get teachers dropdown | - |

---

## ?? **Teacher Assignments**

| Method | Endpoint | Description | Body |
|--------|----------|-------------|------|
| GET | `/teachers/assignments` | Get all teacher assignments | - |
| POST | `/teachers/assign-subject-arm` | Assign teacher to subject/arm | ?concatParameters={value} |
| DELETE | `/teachers/assignments/subject/{id}` | Unassign subject teacher | - |
| DELETE | `/teachers/assignments/class/{id}` | Unassign class teacher | - |
| GET | `/teachers/{teacherId}/assigned-subjects` | Get assigned subjects for teacher | - |
| GET | `/teachers/{teacherId}/assigned-arms` | Get assigned arms for teacher | - |
| POST | `/teachers/transfer-subject-assignments` | Transfer subject assignments | List<SubjectIdsBto> |
| POST | `/teachers/transfer-arm-assignments` | Transfer arm assignments | List<SubjectIdsBto> |
| GET | `/teachers/subject-teachers/search` | Search subject teacher | ?userName={value} |
| GET | `/teachers/form-masters/search` | Search form master | ?userName={value} |

---

## ?? **School Management**

| Method | Endpoint | Description | Body |
|--------|----------|-------------|------|
| GET | `/school` | Get school information | - |
| PUT | `/school` | Update school information | SchoolInfoDto |
| GET | `/school/labels` | Get school labels | - |
| PUT | `/school/labels` | Update school labels | SchoolLevelCategoriesLabelDto |
| GET | `/school/resumption-date` | Get resumption date | - |
| POST | `/school/resumption-date` | Set resumption date | DateTime |
| POST | `/school/head` | Create school head | SchoolHeadDto |
| GET | `/school/heads` | Get all school heads | - |
| PUT | `/school/head/{id}` | Update school head | SchoolHeadDto |
| POST | `/school/head/assignment` | Create school head assignment | - |
| GET | `/school/head/names` | Get school head names | - |

---

## ?? **Academic Sessions & Terms**

| Method | Endpoint | Description | Body |
|--------|----------|-------------|------|
| GET | `/sessions` | Get all academic sessions | - |
| POST | `/sessions` | Create academic session | AcademicSessionDto |
| PUT | `/sessions/{id}` | Update academic session | AcademicSessionDto |
| DELETE | `/sessions/{id}` | Delete academic session | - |
| GET | `/sessions/current` | Get current sessions and term | - |
| GET | `/sessions/{sessionId}/terms` | Get terms for a session | - |
| POST | `/terms/{termId}/activate` | Activate a term | - |

---

## ?? **Assessment & CA Settings**

| Method | Endpoint | Description | Body |
|--------|----------|-------------|------|
| POST | `/assessment/prepare` | Prepare assessment for whole school | ?requestStatus={value} |
| POST | `/assessment/prepare-arm` | Prepare assessment for arm | ?concatParameters={value} |
| POST | `/assessment/prepare-class` | Prepare assessment for class | ?concatParameters={value} |
| DELETE | `/assessment/clear` | Clear current assessment preparation | - |
| GET | `/assessment/ca-settings` | Get current CA settings | - |
| POST | `/assessment/ca-settings` | Submit CA setting | CasSettingDto |
| GET | `/assessment/pass-mark` | Get pass mark | - |
| POST | `/assessment/pass-mark` | Submit pass mark | byte |
| GET | `/assessment/arm/{armId}` | Get arm student assessment | - |

---

## ?? **Student Promotion**

| Method | Endpoint | Description | Body |
|--------|----------|-------------|------|
| POST | `/students/auto-promotion` | Auto promote JSS3 to SS1 | - |
| POST | `/students/automatic-promotion` | Comprehensive school-wide promotion | - |
| GET | `/promotion/validate` | Validate promotion | - |
| POST | `/promotion/confirm` | Confirm promotion | - |
| POST | `/promotion/undo` | Undo recent promotion | - |

---

## ?? **Elective Subjects**

| Method | Endpoint | Description | Body |
|--------|----------|-------------|------|
| GET | `/electives/access-status` | Get elective access status dropdown | - |
| POST | `/electives/change-selection-status` | Change elective selection status | ?selectionId={value} |
| GET | `/electives/category-students` | Get category students for subject | ?catStatus={}&armId={}&subjectId={} |
| POST | `/electives/assign-to-students` | Assign subject to students | List<StudentIdsDto> |
| POST | `/electives/unassign-from-students` | Unassign subject from students | List<StudentIdsDto> |
| GET | `/electives/arm-students` | Get arm students for subject | ?armId={}&subjectId={} |
| GET | `/electives/arms-dropdown` | Get arms dropdown for subject | ?classId={value} |
| GET | `/electives/subjects-dropdown` | Get elective subjects dropdown | - |

---

## ?? **Attendance**

| Method | Endpoint | Description | Body |
|--------|----------|-------------|------|
| POST | `/attendance/total` | Create total attendance | int |
| GET | `/attendance/total` | Get current total attendance | - |

---

## ?? **Dashboard**

| Method | Endpoint | Description | Body |
|--------|----------|-------------|------|
| GET | `/dashboard/summary` | Get school summary dashboard | - |

---

## ?? **Common Query Parameters**

| Parameter | Type | Used In | Example |
|-----------|------|---------|---------|
| `admissionNo` | string | Student search | `?admissionNo=STU001` |
| `searchParameter` | string | Various searches | `?searchParameter=john` |
| `status` | enum | Subject filtering | `?status=General` |
| `userName` | string | Teacher search | `?userName=smith` |
| `armStatus` | enum | Class arms | `?armStatus=A` |
| `classId` | string | Various | `?classId=123` |
| `subjectId` | string | Various | `?subjectId=456` |
| `armId` | string | Various | `?armId=789` |
| `requestStatus` | byte? | Assessment | `?requestStatus=1` |
| `concatParameters` | string | Legacy params | `?concatParameters=...` |

---

## ?? **Common Request Bodies**

### **ClassDto**
```json
{
  "id": "string",
  "className": "Primary1",
  "classLabel": "Primary 1",
  "schoolInfoId": "string"
}
```

### **StudentDetailsDto**
```json
{
  "id": "string",
  "idNo": "STU001",
  "firstName": "John",
  "middleName": "David",
  "surName": "Doe",
  "dateOfBirth": "2010-01-15",
  "gender": "Male",
  "currentClass": "Primary3",
  "classArmId": "string",
  "guardianName": "Jane Doe",
  "mobileNumber": "+234..."
}
```

### **SubjectDto**
```json
{
  "id": "string",
  "subjectName": "Mathematics",
  "subjectStatus": "General",
  "timeStatus": "Current"
}
```

### **UserDto (Teacher)**
```json
{
  "id": "string",
  "firstName": "Mary",
  "lastName": "Smith",
  "email": "mary@school.com",
  "phoneNumber": "+234...",
  "employeeStatus": "Active"
}
```

---

## ?? **HTTP Status Codes**

| Code | Meaning | When Used |
|------|---------|-----------|
| 200 | OK | Successful GET, PUT, DELETE |
| 201 | Created | Successful POST (resource created) |
| 400 | Bad Request | Invalid model, validation failed |
| 404 | Not Found | Resource doesn't exist |
| 499 | Client Closed Request | Operation cancelled |
| 500 | Internal Server Error | Server-side error |

---

## ?? **Authentication**

```http
Authorization: Bearer {your-jwt-token}
```

**Note:** Add authentication headers to all requests once authentication is implemented.

---

## ?? **Response Format**

### **Success Response**
```json
{
  "message": "Operation successful",
  "data": { /* result data */ },
  "count": 10
}
```

### **Error Response**
```json
{
  "message": "An error occurred",
  "errors": {
    "field1": ["Error message 1"],
    "field2": ["Error message 2"]
  }
}
```

---

## ?? **Usage Examples**

### **Example 1: Get All Classes**
```bash
GET https://api.school.com/api/registry/classes
```

### **Example 2: Create a Student**
```bash
POST https://api.school.com/api/registry/students
Content-Type: application/json

{
  "idNo": "STU001",
  "firstName": "John",
  "surName": "Doe",
  "currentClass": "Primary3",
  "classArmId": "123"
}
```

### **Example 3: Search Students**
```bash
GET https://api.school.com/api/registry/students/search?admissionNo=STU001
```

### **Example 4: Auto Promote Students**
```bash
POST https://api.school.com/api/registry/students/auto-promotion
```

### **Example 5: Update School Info**
```bash
PUT https://api.school.com/api/registry/school
Content-Type: application/json

{
  "schoolName": "ABC School",
  "address": "123 Street",
  "phoneNumber": "+234..."
}
```

---

## ?? **Postman Collection**

Import the following into Postman for quick testing:

```json
{
  "info": {
    "name": "Registry API",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "item": [
    {
      "name": "Classes",
      "item": [
        {
          "name": "Get All Classes",
          "request": {
            "method": "GET",
            "url": "{{baseUrl}}/api/registry/classes"
          }
        },
        {
          "name": "Create Class",
          "request": {
            "method": "POST",
            "url": "{{baseUrl}}/api/registry/classes",
            "body": {
              "mode": "raw",
              "raw": "{\n  \"className\": \"Primary1\",\n  \"classLabel\": \"Primary 1\"\n}"
            }
          }
        }
      ]
    }
  ],
  "variable": [
    {
      "key": "baseUrl",
      "value": "https://localhost:5001"
    }
  ]
}
```

---

## ?? **Quick Tips**

1. **All endpoints support cancellation** - Pass `CancellationToken` from client
2. **Use appropriate HTTP verbs** - GET for read, POST for create, PUT for update, DELETE for delete
3. **Check ModelState** - Ensure request body is valid before calling
4. **Handle errors gracefully** - Check status codes and error messages
5. **Log operations** - Use structured logging for debugging
6. **Test with Postman** - Import collection for quick testing

---

**Last Updated:** January 2025  
**API Version:** v1  
**Base URL:** `https://api.school.com/api/registry`  
**Status:** ? Production Ready  
