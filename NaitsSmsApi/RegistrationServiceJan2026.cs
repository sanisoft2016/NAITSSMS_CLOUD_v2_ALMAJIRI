using AutoMapper;
using Lib;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Naits.Common.Shared;
using Naits.Common.Shared.Dto;
using Naits.Common.Shared.Identity;
using Naits.Model.Entities;
using Naits.Model.Repositories.Base;
using Naits.Model.Repositories.EntityFramework;
using Naits.Services.Base;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Migrations;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Razor.Generator;
using System.Web.Script.Serialization;

namespace Naits.Services.Implementation
{
    public class RegistrationServiceJan2026
    {
        public IEnumerable<ClassDto> GetClass()
        {
            var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var getCollection = classRepository.Where(s => s.SchoolInfoId == schoolId);
            return Mapper.Map<IEnumerable<ClassDto>>(getCollection);
        }

        public bool CreateClass(ClassDto model)
        {
            //var schoolId = GetSchoolId();
            //var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            //var hasSchoolRegistered = schoolInfoRepository.Find(r => r.SchoolRegistrationOfficerId == schoolId);
            //if (hasSchoolRegistered != null)
            //{
            var classsRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            if (model.Id == 0)
            {
                var classList = classsRepository.GetAll().ToList();
                var classId = 1;
                if (classList.Any())
                {
                    classId = classList.Max(s => s.Id) + 1;
                }
                model.Id = classId;
            }
            model.SchoolInfoId = schoolId;
            classsRepository.Insert(Mapper.Map<RxClasss>(model));
            return true;
            //}
            //return false;
        }

        public IEnumerable<ClassArmDto> GetClasssArm(short classId)
        {
            var classArmRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var collections = classArmRepository.Where(a => a.ClassId == classId, a => a.Classs);
            return Mapper.Map<IEnumerable<ClassArmDto>>(collections);
        }

        public bool CreatClassArm(ClassArmDto model)
        {
            var classArmRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            //var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            if (model.Id == 0)
            {
                var armList = classArmRepository.GetAll().ToList();
                var armId = 1;
                if (armList.Any())
                {
                    armId = armList.Max(s => s.Id) + 1;
                }
                model.Id = armId;
            }
            classArmRepository.Insert(Mapper.Map<AxClassArm>(model));
            return true;
        }

        public byte CreatStudent(StudentDetailsDto model)
        {
            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            model.DateOfAdmsn = DateTime.Now;

            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var classRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var actualClass = classRepository.Find(x => x.Id == model.ClassArmId, v => v.Classs).Classs.ClassName;
            model.CurrentClass = actualClass;
            if (!(actualClass == CurrentClass.Ss1 || actualClass == CurrentClass.Ss2 || actualClass == CurrentClass.Ss3))
            {
                model.ClassCategory = GetClassCategoryStatus(actualClass);
            }
            var yearlyAndTermlyStatusId =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>()
                    .Find(
                        x =>
                            x.Session.SchoolInfoId == schoolId &&
                            x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current)?.Id;
            if (yearlyAndTermlyStatusId == null)
            {
                return 1;
            }
            model.SchoolInfoId = GetSchoolIdForSchoolRegistrationOfficer();
            //model.NetworkType = GetTeachersAndOthersService().GetNetworkType(model.MobileNumber);
            model.Id = model.Id ?? Guid.NewGuid().ToString();
            studentDetailRepository.Insert(Mapper.Map<TxStudentDetails>(model));
            //UnitOfWork.SaveChanges();
            var studentId = model.Id;
            //studentDetailRepository.Find(v => v.SchoolInfoId == schoolId && v.IdNo == model.IdNo).Id;

            var studentAcademicProgressHistory =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
            var classId = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>().Find(
                a => a.Id == model.ClassArmId && a.Classs.SchoolInfoId == schoolId,
                s => s.Classs).ClassId;
            ;
            var model2 = new StudentAcademicTxProgressHistory
            {
                YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId,
                ClassId = classId,
                StudentId = studentId,
                ClassCategory = model.ClassCategory //studentModel.ClassCategory
            };
            studentAcademicProgressHistory.Insert(model2);

            var implementationType = GetSystemImplementationStatus();
            if (implementationType == FreePlugIn.Encrypt("0", true))
            {
                var listOfStudentAssessment = new List<StudenXtAccessment>();
                var studentLevelCategory = StudentLevelCategory.IsBasic;
                List<SubjectDto> studentSubjectList;
                if (model2.ClassCategory == ClassCategoryStatus.Science ||
                    model2.ClassCategory == ClassCategoryStatus.Art ||
                    model2.ClassCategory == ClassCategoryStatus.Commercial)
                {
                    studentSubjectList = SeniorClassesSubjectListCurrent(model2.ClassCategory);
                    studentLevelCategory = StudentLevelCategory.IsSenior;
                }
                else
                {
                    studentSubjectList = BasicClassesSubjectListCurrent(model.CurrentClass);
                }
                var studentAssessmentRepository =
                    ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
                if (studentSubjectList.Any())
                {
                    foreach (var eachSubject in studentSubjectList)
                    {
                        var classTeacherId =
                            ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>()
                                .Find(x => x.ArmId == model.ClassArmId && x.SubjectId == eachSubject.Id)
                                ?.EmployeeId;

                        var noOfStudentInTheSelectedArm = 0;
                        if (studentLevelCategory == StudentLevelCategory.IsSenior)
                        {
                            noOfStudentInTheSelectedArm =
                                studentDetailRepository.Count(
                                    x => x.ClassArmId == model.ClassArmId && x.ClassCategory == model.ClassCategory);
                            if (noOfStudentInTheSelectedArm == 1)
                            {
                                if (classTeacherId != null)
                                {
                                    var firstStudentId =
                                        studentDetailRepository.Find(
                                            x =>
                                                x.ClassArmId == model.ClassArmId &&
                                                x.ClassCategory == model.ClassCategory).Id;
                                    var newAssessment = new StudenXtAccessment
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        ClassArmId = model.ClassArmId,
                                        StudentId = firstStudentId,
                                        EmployeeId = (int)classTeacherId,
                                        SubjectId = eachSubject.Id,
                                        LastUpdateDate = DateTime.Now,
                                        Ca1 = 0,
                                        Ca2 = 0,
                                        Ca3 = 0,
                                        Exams = 0,
                                        YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId
                                    };
                                    studentAssessmentRepository.Insert(newAssessment);
                                    listOfStudentAssessment.Add(newAssessment);

                                    var newAssessment2 = new StudenXtAccessment
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        ClassArmId = model.ClassArmId,
                                        StudentId = studentId,
                                        EmployeeId = (int)classTeacherId,
                                        SubjectId = eachSubject.Id,
                                        LastUpdateDate = DateTime.Now,
                                        Ca1 = 0,
                                        Ca2 = 0,
                                        Ca3 = 0,
                                        Exams = 0,
                                        YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId
                                    };
                                    studentAssessmentRepository.Insert(newAssessment2);
                                    listOfStudentAssessment.Add(newAssessment2);
                                }
                                else
                                {
                                    foreach (var studentAccessment in listOfStudentAssessment)
                                    {
                                        studentAssessmentRepository.Delete(studentAccessment);
                                    }
                                    studentAcademicProgressHistory.Delete(model2);
                                    studentDetailRepository.Delete(model.Id);

                                    return 2; //Teacher has not been assigned to some subjects in the selected class-Arm
                                }
                            }
                        }
                        else
                        {
                            //Here is for basic student
                            noOfStudentInTheSelectedArm =
                                studentDetailRepository.Count(x => x.ClassArmId == model.ClassArmId);
                            if (noOfStudentInTheSelectedArm == 1)
                            {
                                if (classTeacherId != null)
                                {
                                    var firstStudentId =
                                        studentDetailRepository.Find(x => x.ClassArmId == model.ClassArmId).Id;
                                    var newAssessment1 = new StudenXtAccessment
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        ClassArmId = model.ClassArmId,
                                        StudentId = firstStudentId,
                                        EmployeeId = (int)classTeacherId,
                                        SubjectId = eachSubject.Id,
                                        LastUpdateDate = DateTime.Now,
                                        Ca1 = 0,
                                        Ca2 = 0,
                                        Ca3 = 0,
                                        Exams = 0,
                                        YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId
                                    };
                                    studentAssessmentRepository.Insert(newAssessment1);
                                    listOfStudentAssessment.Add(newAssessment1);

                                    var newAssessment2 = new StudenXtAccessment
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        ClassArmId = model.ClassArmId,
                                        StudentId = studentId,
                                        EmployeeId = (int)classTeacherId,
                                        SubjectId = eachSubject.Id,
                                        LastUpdateDate = DateTime.Now,
                                        Ca1 = 0,
                                        Ca2 = 0,
                                        Ca3 = 0,
                                        Exams = 0,
                                        YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId
                                    };
                                    studentAssessmentRepository.Insert(newAssessment2);
                                    listOfStudentAssessment.Add(newAssessment2);
                                }
                                else
                                {
                                    foreach (var studentAccessment in listOfStudentAssessment)
                                    {
                                        studentAssessmentRepository.Delete(studentAccessment);
                                    }
                                    studentAcademicProgressHistory.Delete(model2);
                                    studentDetailRepository.Delete(model.Id);
                                    return 2; //Teacher has not been assigned to some subjects in the selected class-Arm
                                }
                            }
                        }
                        if (noOfStudentInTheSelectedArm > 1)
                        {
                            var newAssessment2 = new StudenXtAccessment
                            {
                                Id = Guid.NewGuid().ToString(),
                                ClassArmId = model.ClassArmId,
                                StudentId = studentId,
                                EmployeeId = (int)classTeacherId,
                                SubjectId = eachSubject.Id,
                                LastUpdateDate = DateTime.Now,
                                Ca1 = 0,
                                Ca2 = 0,
                                Ca3 = 0,
                                Exams = 0,
                                YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId
                            };
                            studentAssessmentRepository.Insert(newAssessment2);
                            listOfStudentAssessment.Add(newAssessment2);
                        }
                        else if (noOfStudentInTheSelectedArm == 0)
                        {
                            break;
                        }
                    }
                }
                //else
                //{
                //    studentAcademicProgressHistory.Delete(model2);
                //    studentDetailRepository.Delete(model.Id);
                //    return 3;//Subject needs to be created first for the selected class category before you can now add students
                //}
            }
            return 0;
        }

        private ITeachersAndOthersService GetTeachersAndOthersService()
        {
            return ServiceLocator.Get<ITeachersAndOthersService>();
        }

        public byte CreatStudent(StudentDetailsDto model, ArmName armName, CurrentClass currentClass)
        {

            var classArmRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var arm =
                classArmRepository.Find(
                    a => a.Classs.ClassName == currentClass && a.ArmName == armName && a.Classs.SchoolInfoId == schoolId,
                    s => s.Classs);
            if (arm != null)
            {
                if (
                    !(currentClass == CurrentClass.Ss1 || currentClass == CurrentClass.Ss2 ||
                      currentClass == CurrentClass.Ss3))
                {
                    model.ClassCategory = GetClassCategoryStatus(currentClass);
                }
                var yearlyAndTermlyStatusId =
                    ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>()
                        .Find(
                            x =>
                                x.Session.SchoolInfoId == schoolId &&
                                x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current)?.Id;

                if (yearlyAndTermlyStatusId == null)
                {
                    return 1;
                }
                //var networkType = GetTeachersAndOthersService().GetNetworkType(model.MobileNumber);
                model.CurrentClass = arm.Classs.ClassName;
                model.StartClass = arm.Classs.ClassLabel;
                model.ClassArmId = arm.Id;
                model.DateOfAdmsn = DateTime.Now;
                model.SchoolInfoId = GetSchoolIdForSchoolRegistrationOfficer();
                model.Id = model.Id ?? Guid.NewGuid().ToString();
                //model.NetworkType = networkType;
                studentDetailRepository.Insert(Mapper.Map<TxStudentDetails>(model));
                // UnitOfWork.SaveChanges();
                var studentId = model.Id;
                //studentDetailRepository.Find(v => v.SchoolInfoId == schoolId && v.IdNo == model.IdNo).Id;
                var studentAcademicProgressHistory =
                    ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
                var model2 = new StudentAcademicTxProgressHistory
                {
                    YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId,
                    ClassId = arm.ClassId,
                    StudentId = studentId,
                    ClassCategory = model.ClassCategory
                };
                studentAcademicProgressHistory.Insert(model2);

                var implementationType = GetSystemImplementationStatus();
                if (implementationType == FreePlugIn.Encrypt("0", true))
                {
                    var listOfStudentAssessment = new List<StudenXtAccessment>();
                    var studentLevelCategory = StudentLevelCategory.IsBasic;
                    List<SubjectDto> studentSubjectList;
                    if (model2.ClassCategory == ClassCategoryStatus.Science ||
                        model2.ClassCategory == ClassCategoryStatus.Art ||
                        model2.ClassCategory == ClassCategoryStatus.Commercial)
                    {
                        studentSubjectList = SeniorClassesSubjectListCurrent(model2.ClassCategory);
                        studentLevelCategory = StudentLevelCategory.IsSenior;
                    }
                    else
                    {
                        studentSubjectList = BasicClassesSubjectListCurrent(model.CurrentClass);
                    }
                    var studentAssessmentRepository =
                        ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
                    if (studentSubjectList.Any())
                    {
                        foreach (var eachSubject in studentSubjectList)
                        {
                            var classTeacherId =
                                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>()
                                    .Find(x => x.ArmId == model.ClassArmId && x.SubjectId == eachSubject.Id)
                                    ?.EmployeeId;

                            var noOfStudentInTheSelectedArm = 0;
                            if (studentLevelCategory == StudentLevelCategory.IsSenior)
                            {
                                noOfStudentInTheSelectedArm =
                                    studentDetailRepository.Count(
                                        x => x.ClassArmId == model.ClassArmId && x.ClassCategory == model.ClassCategory);
                                if (noOfStudentInTheSelectedArm == 1)
                                {
                                    if (classTeacherId != null)
                                    {
                                        var firstStudentId =
                                            studentDetailRepository.Find(
                                                x =>
                                                    x.ClassArmId == model.ClassArmId &&
                                                    x.ClassCategory == model.ClassCategory).Id;
                                        var newAssessment = new StudenXtAccessment
                                        {
                                            Id = Guid.NewGuid().ToString(),
                                            ClassArmId = model.ClassArmId,
                                            StudentId = firstStudentId,
                                            EmployeeId = (int)classTeacherId,
                                            SubjectId = eachSubject.Id,
                                            LastUpdateDate = DateTime.Now,
                                            Ca1 = 0,
                                            Ca2 = 0,
                                            Ca3 = 0,
                                            Exams = 0,
                                            YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId
                                        };
                                        studentAssessmentRepository.Insert(newAssessment);
                                        listOfStudentAssessment.Add(newAssessment);

                                        var newAssessment2 = new StudenXtAccessment
                                        {
                                            Id = Guid.NewGuid().ToString(),
                                            ClassArmId = model.ClassArmId,
                                            StudentId = studentId,
                                            EmployeeId = (int)classTeacherId,
                                            SubjectId = eachSubject.Id,
                                            LastUpdateDate = DateTime.Now,
                                            Ca1 = 0,
                                            Ca2 = 0,
                                            Ca3 = 0,
                                            Exams = 0,
                                            YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId
                                        };
                                        studentAssessmentRepository.Insert(newAssessment2);
                                        listOfStudentAssessment.Add(newAssessment2);
                                    }
                                    else
                                    {
                                        foreach (var studentAccessment in listOfStudentAssessment)
                                        {
                                            studentAssessmentRepository.Delete(studentAccessment);
                                        }
                                        studentAcademicProgressHistory.Delete(model2);
                                        studentDetailRepository.Delete(model.Id);

                                        return 2;
                                        //Teacher has not been assigned to some subjects in the selected class-Arm
                                    }
                                }
                            }
                            else
                            {
                                //Here is for basic student
                                noOfStudentInTheSelectedArm =
                                    studentDetailRepository.Count(x => x.ClassArmId == model.ClassArmId);
                                if (noOfStudentInTheSelectedArm == 1)
                                {
                                    if (classTeacherId != null)
                                    {
                                        var firstStudentId =
                                            studentDetailRepository.Find(x => x.ClassArmId == model.ClassArmId).Id;
                                        var newAssessment1 = new StudenXtAccessment
                                        {
                                            Id = Guid.NewGuid().ToString(),
                                            ClassArmId = model.ClassArmId,
                                            StudentId = firstStudentId,
                                            EmployeeId = (int)classTeacherId,
                                            SubjectId = eachSubject.Id,
                                            LastUpdateDate = DateTime.Now,
                                            Ca1 = 0,
                                            Ca2 = 0,
                                            Ca3 = 0,
                                            Exams = 0,
                                            YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId
                                        };
                                        studentAssessmentRepository.Insert(newAssessment1);
                                        listOfStudentAssessment.Add(newAssessment1);

                                        var newAssessment2 = new StudenXtAccessment
                                        {
                                            Id = Guid.NewGuid().ToString(),
                                            ClassArmId = model.ClassArmId,
                                            StudentId = studentId,
                                            EmployeeId = (int)classTeacherId,
                                            SubjectId = eachSubject.Id,
                                            LastUpdateDate = DateTime.Now,
                                            Ca1 = 0,
                                            Ca2 = 0,
                                            Ca3 = 0,
                                            Exams = 0,
                                            YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId
                                        };
                                        studentAssessmentRepository.Insert(newAssessment2);
                                        listOfStudentAssessment.Add(newAssessment2);
                                    }
                                    else
                                    {
                                        foreach (var studentAccessment in listOfStudentAssessment)
                                        {
                                            studentAssessmentRepository.Delete(studentAccessment);
                                        }
                                        studentAcademicProgressHistory.Delete(model2);
                                        studentDetailRepository.Delete(model.Id);
                                        return 2;
                                        //Teacher has not been assigned to some subjects in the selected class-Arm
                                    }
                                }
                            }
                            if (noOfStudentInTheSelectedArm > 1)
                            {
                                var newAssessment2 = new StudenXtAccessment
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    ClassArmId = model.ClassArmId,
                                    StudentId = studentId,
                                    EmployeeId = (int)classTeacherId,
                                    SubjectId = eachSubject.Id,
                                    LastUpdateDate = DateTime.Now,
                                    Ca1 = 0,
                                    Ca2 = 0,
                                    Ca3 = 0,
                                    Exams = 0,
                                    YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId
                                };
                                studentAssessmentRepository.Insert(newAssessment2);
                                listOfStudentAssessment.Add(newAssessment2);
                            }
                            else if (noOfStudentInTheSelectedArm == 0)
                            {
                                break;
                            }
                        }
                    }
                    //else
                    //{
                    //    studentAcademicProgressHistory.Delete(model2);
                    //    studentDetailRepository.Delete(model.Id);
                    //    return 3;//Subject needs to be created first for the selected class category before you can now add students
                    //}
                }
                return 0;
            }
            return 0;
        }

        public IList<StudentDetailsDto> GetStudentForAnArm(int armId)
        {
            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var collection = studentDetailRepository.Where(s => s.ClassArmId == armId, u => u.User,
                u => u.ClassArm.Classs);
            return Mapper.Map<List<StudentDetailsDto>>(collection);
        }

        public bool DoesThisPreNurserySubjectAreadyExist(string subjectName)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExist =
                subjectRepository.Find(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        sr.SubjectCategory.SubjectStatus == SubjectStatus.PreNursery);
            return doesSubjectExist == null;
        }

        public bool PreNurserySubjectUpdateValidation(string subjectName, int id)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExistAgainstAnotherRecord =
                subjectRepository.Find(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        sr.SubjectCategory.SubjectStatus == SubjectStatus.PreNursery);
            if (doesSubjectExistAgainstAnotherRecord != null)
            {
                if (doesSubjectExistAgainstAnotherRecord.Id == id)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        public bool GeneralSubjectUpdateValidation(string subjectName, int id)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExistAgainstAnotherRecord =
                subjectRepository.Find(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        sr.SubjectCategory.SubjectStatus == SubjectStatus.General);
            if (doesSubjectExistAgainstAnotherRecord != null)
            {
                if (doesSubjectExistAgainstAnotherRecord.Id == id)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        public bool DoesThisGeneralSubjectAreadyExist(string subjectName)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExist =
                subjectRepository.Find(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        sr.SubjectCategory.SubjectStatus != SubjectStatus.General);
            return doesSubjectExist == null;
        }

        public bool NurserySubjectUpdateValidation(string subjectName, int id)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExistAgainstAnotherRecord =
                subjectRepository.Find(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        (sr.SubjectCategory.SubjectStatus == SubjectStatus.Nursery ||
                         sr.SubjectCategory.SubjectStatus == SubjectStatus.General));
            if (doesSubjectExistAgainstAnotherRecord != null)
            {
                if (doesSubjectExistAgainstAnotherRecord.Id == id)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        public bool DoesThisNurserySubjectAreadyExist(string subjectName)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExist =
                subjectRepository.Find(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        (sr.SubjectCategory.SubjectStatus == SubjectStatus.Nursery ||
                         sr.SubjectCategory.SubjectStatus == SubjectStatus.General
                         //sr.SubjectCategory.SubjectStatus == SubjectStatus.Elective
                         
                         ));
            return doesSubjectExist == null;
        }

        public bool PrimarySubjectUpdateValidation(string subjectName, int id)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExistAgainstAnotherRecord =
                subjectRepository.Find(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        (sr.SubjectCategory.SubjectStatus == SubjectStatus.Primary ||
                         sr.SubjectCategory.SubjectStatus == SubjectStatus.General));
            if (doesSubjectExistAgainstAnotherRecord != null)
            {
                if (doesSubjectExistAgainstAnotherRecord.Id == id)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        public bool DoesThisPrimarySubjectAlreadyExist(string subjectName)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExist =
                subjectRepository.Find(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        (sr.SubjectCategory.SubjectStatus == SubjectStatus.Primary ||
                         sr.SubjectCategory.SubjectStatus == SubjectStatus.General
                         //sr.SubjectCategory.SubjectStatus == SubjectStatus.Elective
                         
                         ));
            return doesSubjectExist == null;
        }

        public bool JssSubjectUpdateValidation(string subjectName, int id)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExistAgainstAnotherRecord =
                subjectRepository.Find(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        (sr.SubjectCategory.SubjectStatus == SubjectStatus.Jss ||
                         sr.SubjectCategory.SubjectStatus == SubjectStatus.General));
            if (doesSubjectExistAgainstAnotherRecord != null)
            {
                if (doesSubjectExistAgainstAnotherRecord.Id == id)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        public bool MigratingDb()
        {
            var context = new Naits.Model.Repositories.EntityFramework.AppContext();
            var actualContext = new AppContext2();
            var userId = actualContext.Users.FirstOrDefault(x => x.UserName == "saofficer")?.Id;

            var users = actualContext.Users.ToList();
            foreach (var each in users)
            {
                context.Users.Add(each);
            }
            var actualSchools = actualContext.SchoolInfos.ToList();
            foreach (var each in actualSchools)
            {
                context.SchoolInfos.Add(each);
                each.SchoolRegistrationOfficer.User.UserStatus = UserStatus.SchoolUser;
                context.SchoolInfos.AddOrUpdate();
            }
            var actualClass = actualContext.ClassTxes.ToList();
            foreach (var each in actualClass)
            {
                context.ClassTxes.Add(each);
            }
            var actualArms = actualContext.TxClassArm.ToList();
            foreach (var each in actualArms)
            {
                context.TxClassArm.Add(each);
            }
            var actualStudent = actualContext.StudentsXyDetailses.ToList();
            foreach (var each in actualStudent)
            {
                context.StudentsXyDetailses.Add(each);
            }
            context.SaveChanges();
            var sysAdminUser = context.Users.First(f => f.UserName == "saofficer");
            sysAdminUser.UserStatus = UserStatus.Admin;
            return true;
        }

        public bool DoesThisJssSubjectAlreadyExist(string subjectName)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExist =
                subjectRepository.Find(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        (sr.SubjectCategory.SubjectStatus == SubjectStatus.Jss ||
                         sr.SubjectCategory.SubjectStatus == SubjectStatus.General 
                         //|| sr.SubjectCategory.SubjectStatus == SubjectStatus.Elective
                         
                         ));
            return doesSubjectExist == null;
        }

        public bool AllSeniorSubjectUpdateValidation(string subjectName, int id)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExistAgainstAnotherRecord =
                subjectRepository.Where(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        (
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.General ||
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.AllSeniors ||
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.Science ||
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.Art), s => s.SubjectCategory)?
                    .Select(r => r.SubjectCategory.SubjectStatus)
                    .ToList();
            if (doesSubjectExistAgainstAnotherRecord != null && doesSubjectExistAgainstAnotherRecord.Any())
            {
                if (doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.Art) ||
                    doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.Science) ||
                    doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.AllSeniors) ||
                    doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.General)
                    || doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.Elective))
                {
                    return false;
                }
                return true;
            }
            return true;
        }

        public bool DoesThisAllSeniorsSubjectAlreadyExist(string subjectName)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExist =
                subjectRepository.Where(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        (
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.General
                         || sr.SubjectCategory.SubjectStatus != SubjectStatus.AllSeniors ||
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.Science
                         || sr.SubjectCategory.SubjectStatus != SubjectStatus.Art ||
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.Commercial), s => s.SubjectCategory)
                    ?.Select(r => r.SubjectCategory.SubjectStatus).ToList();
            if (doesSubjectExist != null && doesSubjectExist.Any())
            {
                if (doesSubjectExist.Contains(SubjectStatus.Art) ||
                    doesSubjectExist.Contains(SubjectStatus.Science) ||
                    doesSubjectExist.Contains(SubjectStatus.AllSeniors) ||
                    doesSubjectExist.Contains(SubjectStatus.General)||
                    // doesSubjectExist.Contains(SubjectStatus.Elective) ||
                    doesSubjectExist.Contains(SubjectStatus.Commercial))
                {
                    return false;
                }
                return true;
            }
            return true;
        }

        public bool ScienceSubjectUpdateValidation(string subjectName, int id)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExistAgainstAnotherRecord =
                subjectRepository.Where(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId, s => s.SubjectCategory)?
                    .Select(r => r.SubjectCategory.SubjectStatus).ToList();

            if (doesSubjectExistAgainstAnotherRecord != null && doesSubjectExistAgainstAnotherRecord.Any())
            {
                if (doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.Art) ||
                    doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.AllSeniors) ||
                    doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.General))
                {
                    return false;
                }
                return true;
            }
            return true;
        }

        public bool DoesThisScienceSubjectAlreadyExist(string subjectName)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExist =
                subjectRepository.Where(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        (
                         sr.SubjectCategory.SubjectStatus == SubjectStatus.Science
                         || sr.SubjectCategory.SubjectStatus != SubjectStatus.General ||
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.AllSeniors
                         || sr.SubjectCategory.SubjectStatus != SubjectStatus.Art ||
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.Commercial), s => s.SubjectCategory)
                    ?.Select(r => r.SubjectCategory.SubjectStatus).ToList();
            if (doesSubjectExist != null && doesSubjectExist.Any())
            {
                if (doesSubjectExist.Contains(SubjectStatus.Art) ||
                    doesSubjectExist.Contains(SubjectStatus.Science) ||
                    doesSubjectExist.Contains(SubjectStatus.AllSeniors) ||
                    doesSubjectExist.Contains(SubjectStatus.General) ||
                    doesSubjectExist.Contains(SubjectStatus.Commercial))
                {
                    return false;
                }
                return true;
            }
            return true;
        }

        public bool ArtSubjectUpdateValidation(string subjectName, int id)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExistAgainstAnotherRecord =
                subjectRepository.Where(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId, s => s.SubjectCategory)?
                    .Select(r => r.SubjectCategory.SubjectStatus).ToList();

            if (doesSubjectExistAgainstAnotherRecord != null && doesSubjectExistAgainstAnotherRecord.Any())
            {
                if (doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.Science) ||
                    doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.AllSeniors) ||
                    doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.General)
                    || doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.Commercial))
                {
                    return false;
                }
                return true;
            }
            return true;
        }

        public bool DoesThisArtSubjectAlreadyExist(string subjectName)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExist =
                subjectRepository.Where(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        (
                         sr.SubjectCategory.SubjectStatus == SubjectStatus.Art ||
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.General ||
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.AllSeniors ||
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.Science ||
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.Commercial), s => s.SubjectCategory)?
                    .Select(r => r.SubjectCategory.SubjectStatus)
                    .ToList();
            if (doesSubjectExist != null && doesSubjectExist.Any())
            {
                if (doesSubjectExist.Contains(SubjectStatus.Art) || doesSubjectExist.Contains(SubjectStatus.Science) ||
                    doesSubjectExist.Contains(SubjectStatus.AllSeniors) ||
                    doesSubjectExist.Contains(SubjectStatus.General)
                    || doesSubjectExist.Contains(SubjectStatus.Elective) ||
                    doesSubjectExist.Contains(SubjectStatus.Commercial))
                {
                    return false;
                }
                return true;
            }
            return true;
        }

        //public bool TradeSubjectUpdateValidation(string subjectName, int id)
        //{
        //    var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
        //    var schoolId = GetSchoolId();
        //    var doesSubjectExistAgainstAnotherRecord =
        //        subjectRepository.Where(
        //            sr =>
        //                sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() && sr.SubjectCategory.SchoolId == schoolId)?
        //            .Select(r => r.SubjectCategory.SubjectStatus = r.SubjectCategory.SubjectStatus).ToList();

        //    if (doesSubjectExistAgainstAnotherRecord != null && doesSubjectExistAgainstAnotherRecord.Any())
        //    {
        //        if (doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.Art) ||
        //            doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.AllSeniors) || doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.General)
        //            || doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.Elective) || doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.Science))
        //        {
        //            return false;
        //        }
        //        return true;
        //    }
        //    return true;
        //}
        public bool DoesThisCommercialSubjectAlreadyExist(string subjectName)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExist =
                subjectRepository.Where(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        (
                         sr.SubjectCategory.SubjectStatus == SubjectStatus.Commercial ||
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.General ||
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.AllSeniors ||
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.Science ||
                         sr.SubjectCategory.SubjectStatus != SubjectStatus.Art), s => s.SubjectCategory)?
                    .Select(r => r.SubjectCategory.SubjectStatus)
                    .ToList();
            if (doesSubjectExist != null && doesSubjectExist.Any())
            {
                if (doesSubjectExist.Contains(SubjectStatus.Art) ||
                    doesSubjectExist.Contains(SubjectStatus.Science) ||
                    doesSubjectExist.Contains(SubjectStatus.AllSeniors) ||
                    doesSubjectExist.Contains(SubjectStatus.General) ||
                    doesSubjectExist.Contains(SubjectStatus.Commercial))
                {
                    return false;
                }
                return true;
            }
            return true;
        }

        public IEnumerable<SelectListItem> StudentStatus(ClassCategoryStatus studentStatus)
        {
            var startClas = new List<SelectListItem>
            {
                new SelectListItem {Text = studentStatus.ToString(), Value = ((int) studentStatus).ToString()}
            };
            var enumToList = EnumsAndExtensionMethods.GetEnumToList<ClassCategoryStatus>();
            startClas.AddRange(from item in enumToList
                               where item != studentStatus
                               select new SelectListItem { Text = item.ToString(), Value = ((int)item).ToString() });
            return startClas;
        }

        public bool UpdateClassReArrangement(List<ClassDto> model)
        {
            return true;
        }

        public IEnumerable<ClassDto> GetClassesInSchoolBySchoolId(int schoolInfoId)
        {
            var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var classes = classRepository.Where(c => c.SchoolInfoId == schoolId);
            return Mapper.Map<List<ClassDto>>(classes);
        }

        public bool ElectiveSubjectUpdateValidation(string subjectName, int id)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExist =
                subjectRepository.Where(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower()
                        && sr.SubjectCategory.SubjectStatus ==  SubjectStatus.Elective &&
                        sr.SubjectCategory.SchoolId == schoolId, o => o.SubjectCategory).FirstOrDefault();

            if (doesSubjectExist != null)
            {
                //var doesSubjectExistAgainstAnotherRecord =
                //var statuss =
                //    doesSubjectExist.Select(
                //        r => r.SubjectCategory.SubjectStatus).ToList();
                if (doesSubjectExist.Id != id)
                {//provided the subject-name exist against another elective subject
                    return false;
                }
                else
                {
                    return true;
                }
            }
            return true;
        }

        //public bool DoesThisOtherSubjectAlreadyExist(string subjectName)
        //{
        //    var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
        //    var schoolId = GetSchoolId();
        //    var doesSubjectExist =
        //        subjectRepository.Where(
        //            sr =>
        //                sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() && sr.SubjectCategory.SchoolId == schoolId &&
        //                (sr.SubjectCategory.SubjectStatus != SubjectStatus.Others || sr.SubjectCategory.SubjectStatus != SubjectStatus.General || sr.SubjectCategory.SubjectStatus != SubjectStatus.AllSeniors ||
        //                 sr.SubjectCategory.SubjectStatus != SubjectStatus.Science || sr.SubjectCategory.SubjectStatus != SubjectStatus.Art ||
        //                 sr.SubjectCategory.SubjectStatus != SubjectStatus.Trade || sr.SubjectCategory.SubjectStatus == SubjectStatus.Islam || sr.SubjectCategory.SubjectStatus == SubjectStatus.Christianity))?.Select(r => r.SubjectCategory.SubjectStatus).ToList();
        //    if (doesSubjectExist != null && doesSubjectExist.Any())
        //    {
        //        if (doesSubjectExist.Contains(SubjectStatus.Art) || doesSubjectExist.Contains(SubjectStatus.Science) ||
        //            doesSubjectExist.Contains(SubjectStatus.AllSeniors) || doesSubjectExist.Contains(SubjectStatus.General)
        //            || doesSubjectExist.Contains(SubjectStatus.Islam) || doesSubjectExist.Contains(SubjectStatus.Christianity)
        //            || doesSubjectExist.Contains(SubjectStatus.Trade) || doesSubjectExist.Contains(SubjectStatus.Others))
        //        {
        //            return false;
        //        }
        //        return true;
        //    }
        //    return true;
        //}

        public IEnumerable<EmployeeDto> GetTeacherByUserName(string searchParameter)
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var teachers =
                teacherRepository.Where(
                    t =>
                        t.SchoolId == schoolId &&
                        (t.User.UserName.Trim().ToLower().Contains(searchParameter.Trim().ToLower())
                         || t.User.FirstName.Trim().ToLower().Contains(searchParameter.Trim().ToLower())
                         || t.User.MiddleName.Trim().ToLower().Contains(searchParameter.Trim().ToLower())
                         || t.User.SurName.Trim().ToLower().Contains(searchParameter.Trim().ToLower())),
                    t => t.User);
            return Mapper.Map<List<EmployeeDto>>(teachers);
        }

        public IEnumerable<TeacherSubjectArmAssignmentDto> GetSubjectTeacherByUserName(string searchParameter)
        {
            var teacherSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var teacherSubjectArmAssignmentList =
                teacherSubjectArmAssignmentRepository.Where(
                    u =>
                        u.Employee.SchoolId == schoolId &&
                        (u.Employee.User.UserName.Trim().ToLower().Contains(searchParameter.Trim().ToLower())
                         || u.Employee.User.FirstName.Trim().ToLower().Contains(searchParameter.Trim().ToLower())
                         || u.Employee.User.MiddleName.Trim().ToLower().Contains(searchParameter.Trim().ToLower())
                         || u.Employee.User.SurName.Trim().ToLower().Contains(searchParameter.Trim().ToLower())),
                    u => u.Employee.User, r => r.Arm.Classs, r => r.Subject);
            return Mapper.Map<List<TeacherSubjectArmAssignmentDto>>(teacherSubjectArmAssignmentList);
        }

        public IEnumerable<TeacherClassArmAssignmentDto> GetFormMasterByUserName(string searchParameter)
        {
            var teacherClassArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var teacherClassArmAssignmentList =
                teacherClassArmAssignmentRepository.Where(
                    u =>
                        u.Employee.SchoolId == schoolId &&
                        (u.Employee.User.UserName.Trim().ToLower().Contains(searchParameter)
                         || u.Employee.User.FirstName.Trim().ToLower().Contains(searchParameter)
                         || u.Employee.User.MiddleName.Trim().ToLower().Contains(searchParameter)
                         || u.Employee.User.SurName.Trim().ToLower().Contains(searchParameter)),
                    u => u.Employee.User, r => r.Arm.Classs);
            return Mapper.Map<List<TeacherClassArmAssignmentDto>>(teacherClassArmAssignmentList);
        }

        public bool SchoolIdValidation(short schoolRegId, string schoolRegistrationOfficerId)
        {
            var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var isSchoolRegIdExist = schoolInfoRepository.Find(si => si.SsNo == schoolRegId);
            if (isSchoolRegIdExist != null)
            {
                if (isSchoolRegIdExist.SchoolRegistrationOfficerId == schoolRegistrationOfficerId)
                {
                    return true;
                }
                return false;
            }
            return true;
        }

        //public ReturnCreateSchoolRegistrarDto CreateSchoolRegistrar(UserDto model)
        //{
        //    var schoolRegistrationOfficerRepository =
        //        ServiceLocator.Get<IGenericRepository<SchoolRegistrationOfficer>>();
        //    var mapResult = Mapper.Map<SchoolRegistrationOfficer>(model);

        //    mapResult.AgentUserId = Thread.CurrentPrincipal.Identity.GetUserId();
        //    schoolRegistrationOfficerRepository.Insert(mapResult);
        //    UnitOfWork.Commit();
        //    var schoolRegistrationOfficerId = schoolRegistrationOfficerRepository.GetAll().Max(b => b.Id);

        //    return new ReturnCreateSchoolRegistrarDto
        //    {
        //        CreatorStatus = creatorStatus,
        //        Status = true,
        //        RegistrationOfficerId = schoolRegistrationOfficerId
        //    };
        //}
        public ReturnCreateSchoolRegistrarDto CreateSchoolRegistrar(UserDto model)
        {
            var schoolRegistrationOfficerRepository =
                ServiceLocator.Get<IGenericRepository<SchoolRegistrationOfficer>>();
            var mapResult = Mapper.Map<SchoolRegistrationOfficer>(model);

            var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            var agentId =
                ServiceLocator.Get<IGenericRepository<Agent, short>>().Find(c => c.UserId == userId)?.Id;

            byte creatorStatus = 0;
            if (agentId != null)
            {
                if (Thread.CurrentPrincipal.IsInRole("SAOfficer"))
                {
                    creatorStatus = 1;
                }
                else if (Thread.CurrentPrincipal.IsInRole("RemoteAgents"))
                {
                    creatorStatus = 2;
                }
                else
                {
                    creatorStatus = 3;
                }
                mapResult.AgentUserId = (short)agentId;
            }
            else
            {
                agentId =
                    ServiceLocator.Get<IGenericRepository<Agent, short>>()
                        .Find(x => x.User.UserName.Trim().ToLower() == "saofficer")
                        .Id;
                mapResult.AgentUserId = (short)agentId;
                creatorStatus = 4; // School registrar creates it
            }
            mapResult.Id = mapResult.Id ?? Guid.NewGuid().ToString();
            var schoolRegistrationOfficerId = mapResult.Id;
            schoolRegistrationOfficerRepository.Insert(mapResult);

            UnitOfWork.Commit();

            return new ReturnCreateSchoolRegistrarDto
            {
                CreatorStatus = creatorStatus,
                Status = true,
                RegistrationOfficerId = schoolRegistrationOfficerId
            };
        }

        //public bool HasSchoolBeenRegistered()
        //{
        //    var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
        //    var schoolId = GetSchoolId();
        //    var hasSchoolRegistered = schoolInfoRepository.Find(r => r.SchoolRegistrationOfficerId == schoolId);
        //    if (hasSchoolRegistered != null)
        //    {
        //        return true;
        //    }
        //    return false;
        //}

        //public StudentAssessmentAndStatusDto GetStudentInAnArmForAssessment(int armId)
        //{
        //    var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
        //    var collection = studentDetailRepository.Where(s => s.ClassArmId == armId);
        //    var teacherClassArmAssignmentRepository =
        //        ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
        //    var schoolId = GetSchoolId();
        //    var assessmentStatus =
        //        teacherClassArmAssignmentRepository.Find(s => s.ArmId == armId && s.ClassTeacher.SchoolId == schoolId).ArmTermlyAssessmentStatus;
        //    var studentAssessmentAndStatusModel = ServiceLocator.Get<StudentAssessmentAndStatusDto>();
        //    studentAssessmentAndStatusModel.ArmTermlyAssessmentStatus = assessmentStatus;
        //    studentAssessmentAndStatusModel.StudentDetails = Mapper.Map<List<StudentDetailsDto>>(collection);
        //    return studentAssessmentAndStatusModel;
        //}

        public IList<SubjectDto> GetSubjects(SubjectStatus subjectStatus)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var collection =
                subjectRepository.Where(
                    sr => sr.SubjectCategory.SubjectStatus == subjectStatus && sr.SubjectCategory.SchoolId == schoolId,
                    s => s.SubjectCategory)?.ToList();
            return Mapper.Map<IList<SubjectDto>>(collection);
        }

        public bool CreatClassSubject(SubjectDto model, SubjectStatus subjectStatus)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var subjectCategoryRepository = ServiceLocator.Get<IGenericRepository<SubjectRxCategories, short>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();


            //var schoolSubjectCategoryRepository = ServiceLocator.Get<IGenericRepository<SubjectRxCategories, short>>();
            //var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            //var schoolModel = ServiceLocator.Get<SchoolTyInfo>();
            var newSNo = (short)(subjectCategoryRepository.Count() + 1);

            var subjectCategory =
                subjectCategoryRepository.Find(u => u.SchoolId == schoolId && u.SubjectStatus == subjectStatus);
            RxSubject mapResult;
            if (subjectCategory != null)
            {
                model.SubjectCategoryId = subjectCategory.Id;
                mapResult = Mapper.Map<RxSubject>(model);
                subjectRepository.Insert(mapResult);
                return true;
            }
            var subjectCategoryList = new List<SubjectRxCategories>
            {
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.PlayClass,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.PreNursery,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Nursery,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.General,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Primary,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Jss,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.AllSeniors,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Science,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Art,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Commercial,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Elective,
                    SchoolId = schoolId,
                    Id = newSNo,
                }
            };
            foreach (var each in subjectCategoryList)
            {
                var getSubjectCategories = subjectCategoryRepository.GetAll();
                var sNo = subjectCategoryRepository.Count();
                var counter = 0;
                var subjectCategories = getSubjectCategories as IList<SubjectRxCategories> ??
                                        getSubjectCategories.ToList();
                if (subjectCategories.Any())
                {
                    var lastRecordId = subjectCategories.OrderBy(r => r.Id).Last().Id;
                    if (lastRecordId != sNo)
                    {
                        foreach (var eachSession in subjectCategories)
                        {
                            counter = counter + 1;
                            if (counter != eachSession.Id)
                            {
                                each.Id = (short)counter;
                                subjectCategoryRepository.Insert(each);
                                UnitOfWork.SaveChanges();
                                break;
                            }
                        }
                    }
                    else
                    {
                        sNo = sNo + 1;
                        each.Id = (short)sNo;
                        subjectCategoryRepository.Insert(each);
                        UnitOfWork.SaveChanges();
                    }
                }
                else
                {
                    sNo = sNo + 1;
                    each.Id = (short)sNo;
                    subjectCategoryRepository.Insert(each);
                    UnitOfWork.SaveChanges();
                }

            }
            var subjectCategoryId =
                subjectCategoryRepository.Find(u => u.SchoolId == schoolId && u.SubjectStatus == subjectStatus).Id;
            model.SubjectCategoryId = subjectCategoryId;
            mapResult = Mapper.Map<RxSubject>(model);
            subjectRepository.Insert(mapResult);
            return true;
        }

        public bool IsClassArmAlreadyExist(ArmName armName, int classId)
        {
            var classArmRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var check = classArmRepository.Find(a => a.ArmName == armName && a.ClassId == classId);
            if (check != null) return false;
            return true;
        }

        public bool IsClassAlreadyExist(CurrentClass className)
        {
            var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var check = classRepository.Find(c => c.ClassName == className && c.SchoolInfoId == schoolId);
            return check == null;
        }

        public byte ValidationChecks(string identificationNo, CurrentClass currentClass, ArmName arm,
            ClassCategoryStatus classCategory)
        {
            var classArmRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var isIdNoAreadyExist =
                studentDetailRepository.Find(
                    s => s.IdNo.Trim().ToLower() == identificationNo.Trim().ToLower() && s.SchoolInfoId == schoolId);
            if (isIdNoAreadyExist != null)
            {
                return 1;
            }
            //classLabel has class Id as its value.
            //Class Id is enough.No need for school Id;
            var isClassAlreadyRegistered =
                classRepository.Find(c => c.SchoolInfoId == schoolId && c.ClassName == currentClass);
            if (isClassAlreadyRegistered == null)
            {
                return 2;
            }
            var isArmAlreadyRegistered =
                classArmRepository.Find(
                    a => a.ArmName == arm && a.Classs.ClassName == currentClass && a.Classs.SchoolInfoId == schoolId);
            if (isArmAlreadyRegistered == null)
            {
                return 3;
            }
            if (currentClass == CurrentClass.Ss1 || currentClass == CurrentClass.Ss2 || currentClass == CurrentClass.Ss3)
            {
                if (
                    !(classCategory == ClassCategoryStatus.Science || classCategory == ClassCategoryStatus.Art ||
                      classCategory == ClassCategoryStatus.Commercial))
                {
                    return 5; //Appropriate class category needs to be selected (Science or Art or Commercial)
                }
            }
            else if (classCategory == ClassCategoryStatus.Science || classCategory == ClassCategoryStatus.Art ||
                     classCategory == ClassCategoryStatus.Commercial)
            {
                if (!(currentClass == CurrentClass.Ss1 || currentClass == CurrentClass.Ss2 ||
                      currentClass == CurrentClass.Ss3))
                {
                    return 5; //Appropriate class category needs to be selected (Science or Art or Commercial)
                }

            }
            return 4;
        }

        public bool IsSubjectAlreadyExist(string subjectName)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var check =
                subjectRepository.Find(
                    s =>
                        s.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        s.SubjectCategory.SchoolId == schoolId);
            return check == null;
        }

        public IList<ClassArmDto> GetAllClasssAndArm(ArmName armStatus = ArmName.O)
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var classArmRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            return
                Mapper.Map<List<ClassArmDto>>(armStatus == ArmName.O
                    ? classArmRepository.Where(r => r.Classs.SchoolInfoId == schoolId)
                    : classArmRepository.Where(a => a.ArmName == armStatus && a.Classs.SchoolInfoId == schoolId));
        }

        public IList<StudentDetailsDto> GetRecentlySavedStudents()
        {
            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var collections =
                studentDetailRepository.Where(
                    t =>
                        (t.Status == Common.Shared.StudentStatus.Fresher ||
                         t.Status == Common.Shared.StudentStatus.Returning) && t.SchoolInfoId == schoolId,
                    s => s.ClassArm.Classs, s => s.User)?.OrderByDescending(s => s.DateOfAdmsn).Take(10)?.ToList();
            return Mapper.Map<IList<StudentDetailsDto>>(collections);
        }

        public bool IsStudentIdAlreadyExist(string idNo)
        {
            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var check = studentDetailRepository.Find(sid => sid.IdNo.Trim().ToLower() == idNo.Trim().ToLower());
            return check == null;
        }

        public bool CreateClassTeacher(UserDto model)
        {
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();

            var teacherId = 0;

            TxEmployee mapResult = new TxEmployee();
            if (int.TryParse(model.Id, out teacherId))
            {
                if (model.Id == "0")
                {
                    var teacherList = teacherRepository.GetAll().ToList();
                    if (teacherList.Any())
                    {
                        mapResult.Id = teacherList.Max(s => s.Id) + 1;
                    }
                    else
                    {
                        mapResult.Id = teacherId;
                    }
                }

            }
            else
            {
                var teacherList = teacherRepository.GetAll().ToList();
                if (teacherList.Any())
                {
                    mapResult.Id = teacherList.Max(s => s.Id) + 1;
                }
                else
                {
                    mapResult.Id = 1;
                }
            }
            mapResult.DateOfBirth = model.DateOfBirth;
            mapResult.UserId = model.UserId;
            mapResult.TimeStatus = model.TimeStatus;
            mapResult.SignatureImageString = model.SignatureImageString;
            mapResult.EmployeeStatus = model.EmployeeStatus;
            mapResult.SchoolId = model.SchoolRegId;
            mapResult.EmployeeIdNo = model.EmployeeIdNo;
            mapResult.FullAddress = model.Addresses;

            if (model.UserName.StartsWith("SuperAccountant"))
            //The case where Super Accountant was being created upon the current school
            {
                //var schoolId = model.SchoolRegId;
                var yearlyAndTermlyStatusRepository =
                    ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();

                var currentTermId =
                    yearlyAndTermlyStatusRepository.Find(
                        x =>
                            x.Session.SchoolInfoId == model.SchoolRegId &&
                            x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current)?.Id;
                if (currentTermId == null)
                {
                    return false;
                }
                var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
                var schoolModel = schoolInfoRepository.Find(x => x.SchoolRegistrationOfficerId == model.SchoolRegId);
                schoolModel.SchoolAdminAccountantStatus = SchoolAdminAccountantStatus.Exist;
                schoolModel.AccountingStartTermId = currentTermId;
                schoolInfoRepository.Update(schoolModel);
                mapResult.SchoolId = model.SchoolRegId;
            }
            else if (model.EmployeeStatus == Common.Shared.EmployeeStatus.Accountant)
            //The case where Subordinate Accountant was being created upon the current school
            {
                mapResult.SchoolId = CurrentTeacherModel().SchoolId;
            }
            else
            {
                mapResult.SchoolId = GetSchoolIdForSchoolRegistrationOfficer();
            }
            teacherRepository.Insert(mapResult);
            return true;
        }

        public StudentDetailsDto GetStudentDetails(string id)
        {
            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var model = studentDetailRepository.Find(s => s.Id == id && s.SchoolInfoId == schoolId,
                s => s.ClassArm.Classs, s => s.User);
            return Mapper.Map<StudentDetailsDto>(model);
        }

        public List<SelectListItem> GetSubjectForDropDown()
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var subjects =
                subjectRepository.Where(s => s.SubjectCategory.SchoolId == schoolId, s => s.SubjectCategory)?
                    .GroupBy(r => r.SubjectName)
                    .Select(r => r.First())
                    .Select(c => new SelectListItem { Value = c.SubjectName, Text = c.SubjectName }).ToList();
            return subjects;
        }

        public SubjectTeacherAssignmentDto GetTeachersClassesAndSujects()
        {
            var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var teachers =
                teacherRepository.Where(
                    t =>
                        t.TimeStatus == Common.Shared.TimeStatus.Current && t.SchoolId == schoolId &&
                        t.EmployeeStatus == Common.Shared.EmployeeStatus.Teacher, u => u.User)?
                    .Select(r => new SelectListItem { Value = r.UserId, Text = r.User.FullName });

            var classes =
                classRepository.Where(s => s.SchoolInfoId == schoolId)?
                    .OrderBy(x => x.ClassName)
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.ClassLabel.ToString() });
            var subjects =
                subjectRepository.Where(s => s.SubjectCategory.SchoolId == schoolId)?
                    .GroupBy(r => r.SubjectName)
                    .Select(r => r.First())
                    .ToList()
                    .Select(c => new SelectListItem { Value = c.SubjectName, Text = c.SubjectName });
            //return new SubjectTeacherAssignmentDto { TeachersName = teachers, ClassName = classes, SubjectsName = subjects };
            var returnResult = ServiceLocator.Get<SubjectTeacherAssignmentDto>();
            returnResult.ClassName = classes;
            returnResult.SubjectsName = subjects;
            returnResult.TeachersName = teachers;
            return returnResult;
        }

        /// <summary>
        /// The function below handle both teacherSubjectArmAssignment and teacherArmAssignment (FormMaster)
        /// </summary>
        public ReturnedArmAndMessageForSubjectClassArmAssignmentDto GetArmsByClassIdAgainstSelectSubject(
            string concatValues)
        {
            var parameterList = concatValues.Split(',').ToList();
            var subjectId = int.Parse(parameterList[0]);
            var classId = Convert.ToInt32(parameterList[1]);
            var returnArmList = ServiceLocator.GetList<ClassArmDto>();
            var returnArmCollections = returnArmList.ToList();
            var teacherSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            //below gets total number of arms
            var armRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var arms = armRepository.Where(c => c.ClassId == classId & c.StudentDetails.Any())?.ToList();

            if (arms != null && arms.Any())
            {
                var assignedSubjects =
                    teacherSubjectArmAssignmentRepository.Where(
                        s => s.Arm.ClassId == classId && s.SubjectId == subjectId,
                        a => a.Subject, c => c.Arm.Classs)?.ToList();
                if (assignedSubjects != null && assignedSubjects.Any())
                {
                    var totalNoOfArm = arms.Count();
                    var totalNoOfAssignedArm = assignedSubjects.Count();
                    if (totalNoOfAssignedArm < totalNoOfArm)
                    {
                        var listOfAssignedArmIds = assignedSubjects.Select(r => r.ArmId).ToList();
                        foreach (var eachArm in arms)
                        {
                            if (!listOfAssignedArmIds.Contains(eachArm.Id))
                            {
                                returnArmCollections.Add(Mapper.Map<ClassArmDto>(eachArm));
                            }
                        }
                    }
                    else if (totalNoOfAssignedArm == totalNoOfArm)
                    {
                        return new ReturnedArmAndMessageForSubjectClassArmAssignmentDto
                        {
                            Message = "All the Subjects has already been assigned to teacher(s)."
                        };
                    }
                }
                else if (assignedSubjects != null && assignedSubjects.Count == 0)
                {
                    returnArmCollections.AddRange(Mapper.Map<List<ClassArmDto>>(arms));
                }
                return new ReturnedArmAndMessageForSubjectClassArmAssignmentDto { Arms = returnArmCollections };
            }
            return new ReturnedArmAndMessageForSubjectClassArmAssignmentDto { Message = "No Arm in the selected class." };
        }

        //public ReturnedArmAndMessageForSubjectClassArmAssignmentDto GetArmByClassIdAgainstSelectSubject2Nd(string concatValues)
        //{
        //    //teacherSubjectArmAssignment
        //    var parameterList = concatValues.Split(',').ToList();

        //    var subjectName = parameterList[0];
        //    var classId = Convert.ToInt32(parameterList[1]);
        //    var returnArmList = ServiceLocator.GetList<ClassArmDto>();
        //    var returnArmCollections = returnArmList.ToList();
        //    var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
        //    var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
        //    var schoolId = GetSchoolId();
        //    var subjects =
        //        subjectRepository.Where(
        //            r =>
        //                r.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
        //                r.SubjectCategory.SchoolId == schoolId, s => s.SubjectCategory)?.ToList();//.SubjectCategory.SubjectStatus;

        //    // More than one class can offer the same Subject.
        //    var classModel = classRepository.Find(classId);
        //    var className = classModel.ClassName;
        //    if (subjects != null && subjects.Any())
        //    {
        //        var subjectsStatus = subjects.Select(r => r.SubjectCategory.SubjectStatus).ToList();
        //        var subjectId = 0;

        //        if (subjectsStatus.Contains(SubjectStatus.Elective))
        //        {
        //            var subjectStudentAssignmentRepository = ServiceLocator.Get<IGenericRepository<SubjectStudentTxAssignment, int>>();
        //            var studentOfferingThisElective =
        //                subjectStudentAssignmentRepository.Where(d => d.StudentDetail.SchoolInfoId == schoolId && d.StudentDetail.TimeStatus == Common.Shared.TimeStatus.Current,
        //                    s => s.StudentDetail.ClassArm.Classs)?
        //                    .Select(
        //                        s =>
        //                           s.StudentDetail.ClassArm.Classs.ClassName
        //                            )
        //                    .ToList();
        //            if (studentOfferingThisElective != null && studentOfferingThisElective.Contains(className))
        //            {
        //                subjectId = subjects.Find(r => r.SubjectCategory.SubjectStatus == SubjectStatus.Elective).Id;
        //                returnArmCollections = GetArmsByClassIdAndSubjectId(subjectId, classId);
        //            }
        //            else
        //            {
        //                return new ReturnedArmAndMessageForSubjectClassArmAssignmentDto
        //                {
        //                    Arms = new List<ClassArmDto>(),
        //                    Message = "Sorry! " + classModel.ClassLabel + " students do not offer this Subject."
        //                };
        //            }
        //        }
        //        if (className == CurrentClass.PreNursery)
        //        {
        //            if (subjectsStatus.Contains(SubjectStatus.PreNursery))
        //            {
        //                subjectId = subjects.Find(r => r.SubjectCategory.SubjectStatus == SubjectStatus.PreNursery).Id;
        //                returnArmCollections = GetArmsByClassIdAndSubjectId(subjectId, classId);
        //            }
        //            else
        //            {
        //                return new ReturnedArmAndMessageForSubjectClassArmAssignmentDto
        //                {
        //                    Arms = new List<ClassArmDto>(),
        //                    Message = "Sorry! " + classModel.ClassLabel + " students do not offer this Subject."
        //                };
        //            }
        //        }
        //        else if (className == CurrentClass.Nursery1 || className == CurrentClass.Nursery2)
        //        {
        //            if (subjectsStatus.Contains(SubjectStatus.Nursery) || subjectsStatus.Contains(SubjectStatus.General))
        //            {
        //                subjectId =
        //                    subjects.Find(
        //                        r =>
        //                            (r.SubjectCategory.SubjectStatus == SubjectStatus.Nursery ||
        //                             r.SubjectCategory.SubjectStatus == SubjectStatus.General)).Id;
        //                returnArmCollections = GetArmsByClassIdAndSubjectId(subjectId, classId);
        //            }
        //            else
        //            {
        //                return new ReturnedArmAndMessageForSubjectClassArmAssignmentDto
        //                {
        //                    Arms = new List<ClassArmDto>(),
        //                    Message = "Sorry! " + classModel.ClassLabel + " students do not offer this Subject."
        //                };
        //            }
        //        }
        //        else if (className == CurrentClass.Primary1 || className == CurrentClass.Primary2
        //            || className == CurrentClass.Primary3 || className == CurrentClass.Primary4
        //            || className == CurrentClass.Primary5 || className == CurrentClass.Primary6)
        //        {
        //            if (subjectsStatus.Contains(SubjectStatus.Primary) || subjectsStatus.Contains(SubjectStatus.General))
        //            {
        //                subjectId =
        //                    subjects.Find(
        //                        r =>
        //                            (r.SubjectCategory.SubjectStatus == SubjectStatus.Primary ||
        //                             r.SubjectCategory.SubjectStatus == SubjectStatus.General
        //                             || r.SubjectCategory.SubjectStatus == SubjectStatus.Elective)).Id;

        //                returnArmCollections = GetArmsByClassIdAndSubjectId(subjectId, classId);
        //            }
        //            else
        //            {
        //                return new ReturnedArmAndMessageForSubjectClassArmAssignmentDto
        //                {
        //                    Arms = new List<ClassArmDto>(),
        //                    Message = "Sorry! " + classModel.ClassLabel + " students do not offer this Subject."
        //                };
        //            }
        //        }
        //        else if (className == CurrentClass.Jss1 || className == CurrentClass.Jss2
        //            || className == CurrentClass.Jss3)
        //        {
        //            if (subjectsStatus.Contains(SubjectStatus.Jss) || subjectsStatus.Contains(SubjectStatus.General))
        //            {
        //                subjectId =
        //                    subjects.Find(
        //                        r =>
        //                            (r.SubjectCategory.SubjectStatus == SubjectStatus.Jss ||
        //                             r.SubjectCategory.SubjectStatus == SubjectStatus.General)).Id;
        //                returnArmCollections = GetArmsByClassIdAndSubjectId(subjectId, classId);
        //            }
        //            else
        //            {
        //                return new ReturnedArmAndMessageForSubjectClassArmAssignmentDto
        //                {
        //                    Arms = new List<ClassArmDto>(),
        //                    Message = "Sorry! " + classModel.ClassLabel + " students do not offer this Subject."
        //                };
        //            }
        //        }
        //        else if (className == CurrentClass.Ss1 || className == CurrentClass.Ss2
        //            || className == CurrentClass.Ss3)
        //        {
        //            if (subjectsStatus.Contains(SubjectStatus.AllSeniors) || subjectsStatus.Contains(SubjectStatus.Science)
        //                || subjectsStatus.Contains(SubjectStatus.Art)
        //                || subjectsStatus.Contains(SubjectStatus.Commercial) || subjectsStatus.Contains(SubjectStatus.General))
        //            {
        //                subjectId =
        //                    subjects.Find(
        //                        r =>
        //                            (r.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors
        //                             || r.SubjectCategory.SubjectStatus == SubjectStatus.Science
        //                             || r.SubjectCategory.SubjectStatus == SubjectStatus.Art
        //                             || r.SubjectCategory.SubjectStatus == SubjectStatus.Commercial
        //                             //|| r.SubjectCategory.SubjectStatus == SubjectStatus.Elective
        //                             || r.SubjectCategory.SubjectStatus == SubjectStatus.General)).Id;
        //                returnArmCollections = GetArmsByClassIdAndSubjectId(subjectId, classId);
        //            }
        //            else
        //            {
        //                return new ReturnedArmAndMessageForSubjectClassArmAssignmentDto
        //                {
        //                    Arms = new List<ClassArmDto>(),
        //                    Message = "Sorry! " + classModel.ClassLabel + " students do not offer this Subject."
        //                };
        //            }
        //        }
        //        return new ReturnedArmAndMessageForSubjectClassArmAssignmentDto
        //        {
        //            Arms = returnArmCollections
        //        };
        //    }
        //    return new ReturnedArmAndMessageForSubjectClassArmAssignmentDto();
        //}

        private List<ClassArmDto> GetArmsByClassIdAndSubjectId(int subjectId, int classId)
        {
            var teacherSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();

            var alreadyAssignedSubjectsAndArms =
                teacherSubjectArmAssignmentRepository.Where(
                    a => a.SubjectId == subjectId && a.Arm.ClassId == classId, a => a.Arm)?.ToList();

            //var teacherSubjectArmAssignments =
            //    alreadyAssignedSubjectsAndArms as IList<TeachTxerSubjectArmAssignment> ??
            //    alreadyAssignedSubjectsAndArms.ToList();
            var classArmRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();

            var assignedArm = (ServiceLocator.GetList<int>()).ToList();

            if (alreadyAssignedSubjectsAndArms != null && alreadyAssignedSubjectsAndArms.Any())
            {
                var armsIds = alreadyAssignedSubjectsAndArms.Select(a => a.Arm.Id).ToList();
                if (armsIds.Count() == 1)
                {
                    assignedArm.Add(armsIds.First());
                }
                else if (armsIds.Count() > 1)
                {
                    assignedArm.AddRange(armsIds);
                }
            }

            var armCollections = classArmRepository.Where(a => a.ClassId == classId && a.StudentDetails.Any());
            var classArms = armCollections as IList<AxClassArm> ?? armCollections.ToList();


            if (classArms.Any() && assignedArm != null && assignedArm.Any())
            {
                var returnArmCollections = new List<ClassArmDto>();
                foreach (var armItem in classArms)
                {
                    //var armId = armItem.Id;
                    if (!assignedArm.Contains(armItem.Id))
                    {
                        returnArmCollections.Add(Mapper.Map<ClassArmDto>(armItem));
                    }
                }
                return returnArmCollections;
            }
            //This return only un-assigned arms.               
            return Mapper.Map<List<ClassArmDto>>(classArms);
        }

        public IList<ClassArmDto> GetArmByClassIdAgainstSelectSubject(string concatValues)
        {
            var classArmRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var teacherClassArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
            var parameterList = concatValues.Split(',').ToList();
            var classId = Convert.ToInt32(parameterList[1]);
            var assignedArm = (ServiceLocator.GetList<int>()).ToList();
            var returnArmList = ServiceLocator.GetList<ClassArmDto>();
            var returnArmCollections = returnArmList.ToList();
            var alreadyAssignedClassArms =
                teacherClassArmAssignmentRepository.Where(a => a.Arm.ClassId == classId)?.ToList();
            if (alreadyAssignedClassArms != null && alreadyAssignedClassArms.Any())
                assignedArm.AddRange(alreadyAssignedClassArms.Select(n => n.ArmId));

            var armCollections = classArmRepository.Where(a => a.ClassId == classId && a.StudentDetails.Any());
            var classArms = armCollections as IList<AxClassArm> ?? armCollections.ToList();

            if (classArms.Any() && assignedArm.Any())
            {
                foreach (var armItem in classArms)
                {
                    if (assignedArm.Any())
                    {
                        var armId = armItem.Id;
                        if (!assignedArm.Contains(armId))
                        {
                            returnArmCollections.Add(Mapper.Map<ClassArmDto>(armItem));
                        }
                    }

                }
            }
            else if (classArms.Any())
            {
                returnArmCollections.AddRange(Mapper.Map<List<ClassArmDto>>(classArms));
            }
            return Mapper.Map<List<ClassArmDto>>(returnArmCollections);
        }

        public IEnumerable<ClassArmDto> AssignTeacherSubjectArm(string concatParameters)
        {
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var classArmRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var teacherClassArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
            var teacherSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            var parameterList = concatParameters.Split(',').ToList();
            var teacherId = parameterList[0];
            var armId = Convert.ToInt32(parameterList[2]);
            //var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            int subjectTeacherId;
            var classId = classArmRepository.Find(armId).ClassId;
            var armList = new List<ClassArmDto>();

            if (string.IsNullOrWhiteSpace(parameterList[1]))
            //condition where parameterList[1] = "" signifies FormMaster Assignment
            {
                subjectTeacherId = teacherRepository.Find(t => t.UserId == teacherId).Id;
                var model = new TeacherClassArmTxAssignment
                {
                    ArmId = armId,
                    EmployeeId = subjectTeacherId,
                    //SchoolInfoId = schoolId
                };
                teacherClassArmAssignmentRepository.Insert(model);
                UnitOfWork.SaveChanges();
                var armIds = new List<int>();

                var alreadyAssignedClassArms =
                    teacherClassArmAssignmentRepository.Where(a => a.Arm.ClassId == classId)?.ToList();
                if (alreadyAssignedClassArms != null && alreadyAssignedClassArms.Any())
                    armIds.AddRange(alreadyAssignedClassArms.Select(n => n.ArmId));

                var getArmsByClassId = classArmRepository.Where(a => a.ClassId == classId && a.StudentDetails.Any());
                var classArmss = getArmsByClassId as IList<AxClassArm> ?? getArmsByClassId.ToList();
                //var armsId = 0;
                if (classArmss.Any() && armIds.Any())
                {
                    foreach (var armItem in classArmss)
                    {
                        var armsId = armItem.Id;
                        if (!armIds.Contains(armsId))
                        {
                            armList.Add(Mapper.Map<ClassArmDto>(armItem));
                        }
                    }
                }
                else if (classArmss.Any())
                {
                    armList.AddRange(Mapper.Map<List<ClassArmDto>>(classArmss));
                }


                var affectiveAndPsychomotorAssessmentRepository =
                    ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();
                var listOfPsychoStudEntries =
                    affectiveAndPsychomotorAssessmentRepository.Where(
                        c =>
                            c.ClassArmId == armId &&
                            c.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current);
                foreach (var each in listOfPsychoStudEntries)
                {
                    each.FormMasterId = subjectTeacherId;
                    affectiveAndPsychomotorAssessmentRepository.Update(each);
                }
                UnitOfWork.Commit();
            }
            else
            {
                var subjectId = int.Parse(parameterList[1]);
                if (int.TryParse(teacherId, out subjectTeacherId))
                {
                    var teachersId = Convert.ToInt32(teacherId);
                    subjectTeacherId = teacherRepository.Find(t => t.Id == teachersId).Id;
                }
                else
                {
                    subjectTeacherId = teacherRepository.Find(t => t.UserId == teacherId).Id;
                }
                var assignmentModel = new TeachTxerSubjectArmAssignment
                {
                    ArmId = armId,
                    SubjectId = subjectId,
                    EmployeeId = subjectTeacherId,
                    //SchoolInfoId = schoolId
                };
                teacherSubjectArmAssignmentRepository.Insert(assignmentModel);
                UnitOfWork.SaveChanges();
                armList.AddRange(GetArmsByClassIdAndSubjectId(subjectId, classId));
            }
            return armList;
        }

        public IEnumerable<TeacherSubjectArmAssignmentDto> GetAssignedTeacherClassAndSubject()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var teacherSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            var resultList = teacherSubjectArmAssignmentRepository.Where(s => s.Employee.SchoolId == schoolId);
            return Mapper.Map<IEnumerable<TeacherSubjectArmAssignmentDto>>(resultList);
        }

        public IEnumerable<TeacherSubjectArmAssignmentDto> UnAssignSubjectTeacher(int id)
        {
            var teacherSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            teacherSubjectArmAssignmentRepository.Delete(id);
            UnitOfWork.SaveChanges();
            return GetSubjectTeachers();
        }

        public IEnumerable<TeacherClassArmAssignmentDto> UnAssignClassTeacher(int id)
        {
            var teacherClassArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
            teacherClassArmAssignmentRepository.Delete(id);
            UnitOfWork.SaveChanges();
            return GetClassTeachers();
        }

        private List<SubjectDto> ElectiveSubjectsCollections(int classId, string schoolId)
        {
            var subjectStudentAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<SubjectStudentTxAssignment, int>>();
            var subjectlist = new List<SubjectDto>();
            var electiveSubject =
                subjectStudentAssignmentRepository.Where(
                    d =>
                        d.StudentDetail.SchoolInfoId == schoolId &&
                        d.StudentDetail.ClassArm.ClassId == classId, g => g.Subject)?
                    .GroupBy(c => c.SubjectId)
                    .Select(c => c.First())
                    .Select(s => s.Subject)
                    .ToList();

            if (electiveSubject != null && electiveSubject.Any())
                subjectlist.AddRange(Mapper.Map<List<SubjectDto>>(electiveSubject));
            return subjectlist;
        }

        private List<SubjectDto> SeniorSubjectList(int classId)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var subjectlist =
                Mapper.Map<List<SubjectDto>>(
                    subjectRepository.Where(
                        s =>
                            s.SubjectCategory.SchoolId == schoolId && s.TimeStatus == Common.Shared.TimeStatus.Current &&
                            (
                                s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                                s.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors
                                || s.SubjectCategory.SubjectStatus == SubjectStatus.Science ||
                                s.SubjectCategory.SubjectStatus == SubjectStatus.Art
                                || s.SubjectCategory.SubjectStatus == SubjectStatus.Commercial)).ToList());

            var electiveSubject = ElectiveSubjectsCollections(classId, schoolId);
            if (electiveSubject != null && electiveSubject.Any())
                subjectlist.AddRange(Mapper.Map<List<SubjectDto>>(electiveSubject));
            return subjectlist;
        }

        public List<SubjectDto> SeniorClassesSubjectListCurrent(ClassCategoryStatus studentStatus)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var list = new List<SubjectDto>();


            if (studentStatus == Common.Shared.ClassCategoryStatus.Science)
            {
                list =
                    Mapper.Map<List<SubjectDto>>(
                        subjectRepository.Where(
                            s =>
                                s.SubjectCategory.SchoolId == schoolId &&
                                s.TimeStatus == Common.Shared.TimeStatus.Current && (
                                    s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                                    s.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors
                                    || s.SubjectCategory.SubjectStatus == SubjectStatus.Science)).ToList());
                return list;
            }
            else if (studentStatus == Common.Shared.ClassCategoryStatus.Commercial)
            {
                list =
                    Mapper.Map<List<SubjectDto>>(
                        subjectRepository.Where(
                            s =>
                                s.SubjectCategory.SchoolId == schoolId &&
                                s.TimeStatus == Common.Shared.TimeStatus.Current && (
                                    s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                                    s.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors
                                    || s.SubjectCategory.SubjectStatus == SubjectStatus.Commercial)).ToList());
                return list;
            }
            else if (studentStatus == Common.Shared.ClassCategoryStatus.Art)
            {
                list =
                    Mapper.Map<List<SubjectDto>>(
                        subjectRepository.Where(
                            s =>
                                s.SubjectCategory.SchoolId == schoolId &&
                                s.TimeStatus == Common.Shared.TimeStatus.Current && (
                                    s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                                    s.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors
                                    || s.SubjectCategory.SubjectStatus == SubjectStatus.Art)).ToList());
                return list;
            }
            return list;
        }

        public List<SubjectDto> BasicClassesSubjectListCurrent(CurrentClass className)
        {

            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var subjectList = new List<SubjectDto>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            if (className == CurrentClass.PlayClass)
            {
                var subjectCollections = subjectRepository.Where(
                    s =>
                        s.SubjectCategory.SchoolId == schoolId && s.TimeStatus == Common.Shared.TimeStatus.Current &&
                        (s.SubjectCategory.SubjectStatus == SubjectStatus.PlayClass)).ToList();
                var list = Mapper.Map<List<SubjectDto>>(subjectCollections);
                subjectList.AddRange(list);
            }
            else if (className == CurrentClass.PreNursery)
            {
                var subjectCollections = subjectRepository.Where(
                    s =>
                        s.SubjectCategory.SchoolId == schoolId && s.TimeStatus == Common.Shared.TimeStatus.Current &&
                        (s.SubjectCategory.SubjectStatus == SubjectStatus.PreNursery)).ToList();
                var list = Mapper.Map<List<SubjectDto>>(subjectCollections);
                subjectList.AddRange(list);
            }
            else if (className == CurrentClass.Nursery1 ||
                     className == CurrentClass.Nursery2)
            {
                var subjectCollections = subjectRepository.Where(
                    s =>
                        s.SubjectCategory.SchoolId == schoolId && s.TimeStatus == Common.Shared.TimeStatus.Current &&
                        (s.SubjectCategory.SubjectStatus == SubjectStatus.General
                         || s.SubjectCategory.SubjectStatus == SubjectStatus.Nursery)).ToList();

                var list = Mapper.Map<List<SubjectDto>>(subjectCollections);
                subjectList.AddRange(list);
            }
            else if (className == CurrentClass.Primary1 || className == CurrentClass.Primary2 ||
                     className == CurrentClass.Primary3 || className == CurrentClass.Primary4 ||
                     className == CurrentClass.Primary5 || className == CurrentClass.Primary6)
            {
                var list = Mapper.Map<List<SubjectDto>>(subjectRepository.Where(
                    s =>
                        s.SubjectCategory.SchoolId == schoolId && s.TimeStatus == Common.Shared.TimeStatus.Current &&
                        (s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                         s.SubjectCategory.SubjectStatus == SubjectStatus.Primary)).ToList());
                subjectList.AddRange(list);
            }
            else if (className == CurrentClass.Jss1 || className == CurrentClass.Jss2 || className == CurrentClass.Jss3)
            {
                var list =
                    Mapper.Map<List<SubjectDto>>(
                        subjectRepository.Where(
                            s =>
                                s.SubjectCategory.SchoolId == schoolId &&
                                s.TimeStatus == Common.Shared.TimeStatus.Current && (
                                    s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                                    s.SubjectCategory.SubjectStatus == SubjectStatus.Jss)).ToList());
                subjectList.AddRange(list);
            }
            return subjectList;
        }

        public List<SubjectDto> SeniorClassesSubjectList(ClassCategoryStatus studentStatus, CurrentClass classs)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var list = new List<SubjectDto>();


            if (studentStatus == Common.Shared.ClassCategoryStatus.Science)
            {
                list =
                    Mapper.Map<List<SubjectDto>>(
                        subjectRepository.Where(s => s.SubjectCategory.SchoolId == schoolId && (
                            s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                            s.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors
                            || s.SubjectCategory.SubjectStatus == SubjectStatus.Science)).ToList());
                return list;
            }
            else if (studentStatus == Common.Shared.ClassCategoryStatus.Commercial)
            {
                list =
                    Mapper.Map<List<SubjectDto>>(
                        subjectRepository.Where(s => s.SubjectCategory.SchoolId == schoolId && (
                            s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                            s.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors
                            || s.SubjectCategory.SubjectStatus == SubjectStatus.Commercial)).ToList());
                return list;
            }
            else if (studentStatus == Common.Shared.ClassCategoryStatus.Art)
            {
                list =
                    Mapper.Map<List<SubjectDto>>(
                        subjectRepository.Where(s => s.SubjectCategory.SchoolId == schoolId && (
                            s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                            s.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors
                            || s.SubjectCategory.SubjectStatus == SubjectStatus.Art)).ToList());
                return list;
            }
            return list;
        }

        public List<SubjectDto> BasicClassesSubjectList(CurrentClass className)
        {

            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var subjectList = new List<SubjectDto>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            if (className == CurrentClass.PreNursery)
            {
                var subjectCollections = subjectRepository.Where(
                    s =>
                        s.SubjectCategory.SchoolId == schoolId &&
                        (s.SubjectCategory.SubjectStatus == SubjectStatus.PreNursery)).ToList();
                var list = Mapper.Map<List<SubjectDto>>(subjectCollections);
                subjectList.AddRange(list);
            }
            else if (className == CurrentClass.Nursery1 ||
                     className == CurrentClass.Nursery2)
            {
                var subjectCollections = subjectRepository.Where(
                    s =>
                        s.SubjectCategory.SchoolId == schoolId &&
                        (s.SubjectCategory.SubjectStatus == SubjectStatus.General
                         || s.SubjectCategory.SubjectStatus == SubjectStatus.Nursery)).ToList();

                var list = Mapper.Map<List<SubjectDto>>(subjectCollections);
                subjectList.AddRange(list);
            }
            else if (className == CurrentClass.Primary1 || className == CurrentClass.Primary2 ||
                     className == CurrentClass.Primary3 || className == CurrentClass.Primary4 ||
                     className == CurrentClass.Primary5 || className == CurrentClass.Primary6)
            {
                var list = Mapper.Map<List<SubjectDto>>(subjectRepository.Where(
                    s =>
                        s.SubjectCategory.SchoolId == schoolId &&
                        (s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                         s.SubjectCategory.SubjectStatus == SubjectStatus.Primary)).ToList());
                subjectList.AddRange(list);
            }
            else if (className == CurrentClass.Jss1 || className == CurrentClass.Jss2 || className == CurrentClass.Jss3)
            {
                var list =
                    Mapper.Map<List<SubjectDto>>(
                        subjectRepository.Where(s => s.SubjectCategory.SchoolId == schoolId && (
                            s.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                            s.SubjectCategory.SubjectStatus == SubjectStatus.Jss)).ToList());
                subjectList.AddRange(list);
            }
            return subjectList;
        }

        private List<UnAssignedClassArmSubjectDto> UnAssignedClassArmSubjectList(
            IGrouping<int, TxStudentDetails> eachClass,
            List<SubjectDto> classSubjectCollection)
        {
            var classSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            //Here, eachclass represent 'SS Science' or 'SS Art' or 'SS Commercial'

            var returnList = new List<UnAssignedClassArmSubjectDto>();

            foreach (var subject in classSubjectCollection)
            {
                var isSubjectAlreadybeenAssignedaTeacher =
                    classSubjectArmAssignmentRepository.Find(x => x.SubjectId == subject.Id && x.ArmId == eachClass.Key);
                if (isSubjectAlreadybeenAssignedaTeacher == null)
                {
                    returnList.Add(new UnAssignedClassArmSubjectDto
                    {
                        SubjectName = subject.SubjectName,
                        ClassName = eachClass.First().ClassArm.Classs.ClassName,
                        ClassLabel = eachClass.First().ClassArm.Classs.ClassLabel,
                        ArmName = eachClass.First().ClassArm.ArmName
                    });
                }
            }
            var electiveList = classSubjectArmAssignmentRepository.Where(c => c.ArmId == eachClass.Key).ToList();
            foreach (var eachElective in electiveList)
            {
                var isSubjectAlreadybeenAssignedaTeacher =
                    classSubjectArmAssignmentRepository.Find(x => x.SubjectId == eachElective.SubjectId);
                if (isSubjectAlreadybeenAssignedaTeacher == null)
                {
                    returnList.Add(new UnAssignedClassArmSubjectDto
                    {
                        SubjectName = eachElective.Subject.SubjectName,
                        ClassName = eachClass.First().ClassArm.Classs.ClassName,
                        ClassLabel = eachClass.First().ClassArm.Classs.ClassLabel,
                        ArmName = eachClass.First().ClassArm.ArmName
                    });
                }
            }
            return returnList;
        }


        private string ProceedFromHereForUpdateSeniorStudent(IGrouping<ClassCategoryStatus, TxStudentDetails> eachClass,
            List<SubjectDto> classSubjectCollection, int checkRecentEnviromentPreparationId,
            IGenericRepository<StudenXtAccessment> studentAccessmentRepository, string schoolId)
        {
            var classSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            //Here, eachclass represent 'SS Science' or 'SS Art' or 'SS Commercial'
            foreach (var eachStudent in eachClass)
            {
                List<RxSubject> electiveSubject = null;

                var subjectStudentAssignmentRepository =
                    ServiceLocator.Get<IGenericRepository<SubjectStudentTxAssignment, int>>();
                electiveSubject =
                    subjectStudentAssignmentRepository.Where(
                        d =>
                            d.StudentDetail.SchoolInfoId == schoolId &&
                            d.Subject.TimeStatus == Common.Shared.TimeStatus.Current &&
                            d.StudentDetail.Id == eachStudent.Id, v => v.Subject)?
                        //&& d.Subject.SubjectCategory.SubjectStatus == SubjectStatus.Elective
                        .Select(
                            s =>
                                s.Subject
                        )
                        .ToList();
                if (electiveSubject != null && electiveSubject.Any())
                {
                    classSubjectCollection.AddRange(Mapper.Map<List<SubjectDto>>(electiveSubject));
                }

                foreach (var eachSubject in classSubjectCollection)
                {
                    var armId = eachStudent.ClassArmId;
                    var teacher =
                        classSubjectArmAssignmentRepository.Find(
                            r => r.ArmId == armId && r.SubjectId == eachSubject.Id);
                    if (teacher != null)
                    {
                        var eachStudentAssessment =
                            studentAccessmentRepository.Find(
                                n =>
                                    n.SubjectId == eachSubject.Id &&
                                    n.Student.Id == eachStudent.Id &&
                                    n.YearlyAndTermlyStatus.TermlyAccessmentStatus ==
                                    TermlyAccessmentStatus.Current);
                        if (eachStudentAssessment != null)
                        {
                            //Do nothing
                            eachStudentAssessment.EmployeeId = teacher.EmployeeId;
                            studentAccessmentRepository.Update(eachStudentAssessment);
                        }
                        else
                        {
                            var accessment = new StudenXtAccessment
                            {
                                Id = Guid.NewGuid().ToString(),
                                EmployeeId = teacher.EmployeeId,
                                SubjectId = eachSubject.Id,
                                StudentId = eachStudent.Id,
                                ClassArmId = eachStudent.ClassArmId,
                                LastUpdateDate = DateTime.Now,
                                YearlyAndTermlyStatusId =
                                    checkRecentEnviromentPreparationId,
                                //SchoolInfoId = schoolId,
                                Ca1 = 0,
                                Ca2 = 0,
                                Ca3 = 0,
                                Exams = 0
                            };
                            studentAccessmentRepository.Insert(accessment);
                        }

                    }
                    else
                    {
                        //Teacher is yet to be assigned to 
                        return "'" + eachSubject.SubjectName + "'" +
                               " has not been assigned to a teacher in " +
                               eachStudent.ClassArm.Classs.ClassLabel +
                               eachStudent.ClassArm.ArmName + ".";
                    }
                }
                //Remove elective from collections
                if (electiveSubject != null && electiveSubject.Any())
                    foreach (var each in electiveSubject)
                    {
                        var electiveToRemove = classSubjectCollection.Find(s => s.Id == each.Id);
                        classSubjectCollection.Remove(electiveToRemove);
                    }
            }
            return "Succeed";
        }

        private List<UnAssignedClassArmSubjectDto> ProceedFromHereForBasicStudents(
            IGrouping<int, TxStudentDetails> eachClass,
            List<SubjectDto> classSubjectCollection, string schoolId)
        {
            var returnList = new List<UnAssignedClassArmSubjectDto>();
            var classSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            // Here for Basic Students: PreNursery, Nursery, Primary and JSS 
            var groupByArm = eachClass.GroupBy(g => g.ClassArmId);
            foreach (var eachArmStudents in groupByArm)
            {
                foreach (var eachStudent in eachArmStudents)
                {
                    List<RxSubject> electiveSubject = null;
                    if (eachStudent.ClassArm.Classs.ClassName == CurrentClass.PreNursery ||
                        eachStudent.ClassArm.Classs.ClassName == CurrentClass.PlayClass)
                    //PlayClass and PreNursery does not do elective
                    {

                    }
                    else
                    {
                        var subjectStudentAssignmentRepository =
                            ServiceLocator.Get<IGenericRepository<SubjectStudentTxAssignment, int>>();
                        electiveSubject =
                            subjectStudentAssignmentRepository.Where(
                                d =>
                                    d.StudentDetail.SchoolInfoId == schoolId &&
                                    d.StudentDetail.Id == eachStudent.Id, v => v.Subject.SubjectCategory)?
                                //&& d.Subject.SubjectCategory.SubjectStatus == SubjectStatus.Elective
                                .Select(
                                    s =>
                                        s.Subject
                                )
                                .ToList();
                        if (electiveSubject != null && electiveSubject.Any())
                        {
                            classSubjectCollection.AddRange(Mapper.Map<List<SubjectDto>>(electiveSubject));
                        }
                    }

                    foreach (var eachSubject in classSubjectCollection)
                    {
                        var armId = eachStudent.ClassArmId;
                        var teacher =
                            classSubjectArmAssignmentRepository.Find(
                                r => r.ArmId == armId && r.SubjectId == eachSubject.Id);
                        if (teacher == null)
                        {
                            //Teacher is yet to be assigned to 
                            returnList.Add(new UnAssignedClassArmSubjectDto
                            {
                                SubjectName = eachSubject.SubjectName,
                                ClassLabel = eachStudent.ClassArm.Classs.ClassLabel,
                                ArmName = eachStudent.ClassArm.ArmName
                            });
                        }
                    }
                }
            }
            return returnList;
        }

        private string ProceedFromHereForUpdateBasicStudents(IGrouping<int, TxStudentDetails> eachClass,
            List<SubjectDto> classSubjectCollection, int checkRecentEnviromentPreparationId,
            IGenericRepository<StudenXtAccessment> studentAccessmentRepository, string schoolId)
        {
            var classSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            // Here for Basic Students: PreNursery, Nursery, Primary and JSS 
            var groupByArm = eachClass.GroupBy(g => g.ClassArmId);
            foreach (var eachArmStudents in groupByArm)
            {
                foreach (var eachStudent in eachArmStudents)
                {
                    List<RxSubject> electiveSubject = null;
                    if (eachStudent.ClassArm.Classs.ClassName == CurrentClass.PreNursery ||
                        eachStudent.ClassArm.Classs.ClassName == CurrentClass.PlayClass)
                    //PlayClass and PreNursery does not do elective
                    {

                    }
                    else
                    {
                        var subjectStudentAssignmentRepository =
                            ServiceLocator.Get<IGenericRepository<SubjectStudentTxAssignment, int>>();
                        electiveSubject =
                            subjectStudentAssignmentRepository.Where(
                                d =>
                                    d.StudentDetail.SchoolInfoId == schoolId &&
                                    d.Subject.TimeStatus == Common.Shared.TimeStatus.Current &&
                                    d.StudentDetail.Id == eachStudent.Id, v => v.Subject.SubjectCategory)?
                                //&& d.Subject.SubjectCategory.SubjectStatus == SubjectStatus.Elective
                                .Select(
                                    s =>
                                        s.Subject
                                )
                                .ToList();
                        if (electiveSubject != null && electiveSubject.Any())
                        {
                            classSubjectCollection.AddRange(Mapper.Map<List<SubjectDto>>(electiveSubject));
                        }
                    }

                    foreach (var eachSubject in classSubjectCollection)
                    {
                        var armId = eachStudent.ClassArmId;
                        var teacher =
                            classSubjectArmAssignmentRepository.Find(
                                r => r.ArmId == armId && r.SubjectId == eachSubject.Id);
                        if (teacher != null)
                        {
                            var eachStudentAssessment =
                                studentAccessmentRepository.Find(
                                    n =>
                                        n.SubjectId == eachSubject.Id &&
                                        n.Student.Id == eachStudent.Id && n.Employee.SchoolId == schoolId &&
                                        n.YearlyAndTermlyStatus.TermlyAccessmentStatus ==
                                        TermlyAccessmentStatus.Current);
                            if (eachStudentAssessment != null)
                            {
                                //Do nothing
                                eachStudentAssessment.EmployeeId = teacher.EmployeeId;
                                studentAccessmentRepository.Update(eachStudentAssessment);
                            }
                            else
                            {
                                var accessment = new StudenXtAccessment
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    EmployeeId = teacher.EmployeeId,
                                    SubjectId = eachSubject.Id,
                                    StudentId = eachStudent.Id,
                                    ClassArmId = eachStudent.ClassArmId,
                                    LastUpdateDate = DateTime.Now,
                                    YearlyAndTermlyStatusId =
                                        checkRecentEnviromentPreparationId,
                                    //SchoolInfoId = schoolId,
                                    Ca1 = 0,
                                    Ca2 = 0,
                                    Ca3 = 0,
                                    Exams = 0
                                };
                                studentAccessmentRepository.Insert(accessment);
                            }

                        }
                        else
                        {
                            //Teacher is yet to be assigned to 
                            return "'" + eachSubject.SubjectName + "'" +
                                   " has not been assigned to a teacher in " +
                                   eachArmStudents.First().ClassArm.Classs.ClassLabel +
                                   eachArmStudents.First().ClassArm.ArmName + ".";
                        }
                    }
                    //Remove elective from collections
                    if (electiveSubject != null && electiveSubject.Any())
                        foreach (var each in electiveSubject)
                        {
                            var electiveToRemove = classSubjectCollection.Find(s => s.Id == each.Id);
                            classSubjectCollection.Remove(electiveToRemove);
                        }
                }
            }
            return "Succeed";
        }

        private string ProceedFromHereForInsert(IGrouping<int, TxStudentDetails> eachClass,
            List<SubjectDto> classSubjectCollection, int checkRecentEnviromentPreparationId,
            IGenericRepository<StudenXtAccessment> studentAccessmentRepository, string schoolId)
        {
            var classSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();

            var groupByArm = eachClass.GroupBy(g => g.ClassArmId);
            foreach (var eachArmStudents in groupByArm)
            {
                foreach (var eachStudent in eachArmStudents)
                {
                    if (eachStudent.ClassArm.Classs.ClassName == CurrentClass.PreNursery ||
                        eachStudent.ClassArm.Classs.ClassName == CurrentClass.PlayClass)
                    //PlayClass and PreNursery does not do elective
                    {

                    }
                    else
                    {
                        var subjectStudentAssignmentRepository =
                            ServiceLocator.Get<IGenericRepository<SubjectStudentTxAssignment, int>>();
                        var electiveSubject =
                            subjectStudentAssignmentRepository.Where(
                                d =>
                                    d.StudentDetail.SchoolInfoId == schoolId &&
                                    d.StudentDetail.Id == eachStudent.Id &&
                                    d.Subject.SubjectCategory.SubjectStatus == SubjectStatus.Elective)?
                                .Select(
                                    s =>
                                        s.Subject
                                )
                                .ToList();
                        if (electiveSubject != null && electiveSubject.Any())
                        {
                            classSubjectCollection.AddRange(Mapper.Map<List<SubjectDto>>(electiveSubject));
                        }
                    }

                    foreach (var eachSubject in classSubjectCollection)
                    {
                        var armId = eachStudent.ClassArmId;
                        var teacher =
                            classSubjectArmAssignmentRepository.Find(
                                r => r.ArmId == armId && r.SubjectId == eachSubject.Id);
                        if (teacher != null)
                        {
                            var accessment = new StudentsAccessmentDto
                            {
                                Id = Guid.NewGuid().ToString(),
                                EmployeeId = teacher.EmployeeId,
                                SubjectId = eachSubject.Id,
                                StudentId = eachStudent.Id,
                                ClassArmId = eachStudent.ClassArmId,
                                LastUpdateDate = DateTime.Now,
                                YearlyAndTermlyStatusId =
                                    checkRecentEnviromentPreparationId,
                                //SchoolInfoId = schoolId,
                                Ca1 = 0,
                                Ca2 = 0,
                                Ca3 = 0,
                                Exams = 0
                            };
                            studentAccessmentRepository.Insert(
                                Mapper.Map<StudenXtAccessment>(accessment));
                        }
                        return "'" + eachSubject.SubjectName + "'" +
                               " has not been assigned to a teacher in " +
                               eachArmStudents.First().ClassArm.Classs.ClassLabel +
                               eachArmStudents.First().ClassArm.ArmName + ".";
                    }
                }

            }
            return "Succeed";
        }

        public StudentAssessmentEnvironmentPreparationReturnDto DoSystemInitialPreparationForStudentAccessment(
            TermStatus term)
        {
            var schpplId = GetSchoolIdForSchoolRegistrationOfficer();
            var principalRepository = ServiceLocator.Get<IGenericRepository<SchoolHeadTx, short>>();
            var hasPrincipalRegistered =
                principalRepository.Find(r => r.Status == Common.Shared.TimeStatus.Current && r.SchoolInfoId == schpplId);
            if (hasPrincipalRegistered == null)
                return new StudentAssessmentEnvironmentPreparationReturnDto
                {
                    StatusCode = "Pricipal needs to be registered first."
                };

            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var allStudents =
                studentDetailRepository.Where(
                    t =>
                        (t.Status == Common.Shared.StudentStatus.Fresher ||
                         t.Status == Common.Shared.StudentStatus.Returning) && t.SchoolInfoId == schpplId,
                    r => r.ClassArm.Classs, g => g.SchoolInfo, s => s.User).ToList();
            //if (allStudents == null)
            //    return new StudentAssessmentEnvironmentPreparationReturnDto
            //    {
            //        StatusCode = "No Student! You need to register students first."
            //    };

            //This is check whether they have ever done result proccessing with the software
            //If yes, there is no need for first preparation
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
            ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            var haveTheyEverDoneStudentsAssessment =
                studentAccessmentRepository.Where(a => a.Exams > 40 && a.Employee.SchoolId == schoolId).FirstOrDefault();
            var academicSessionRepository = ServiceLocator.Get<IGenericRepository<TxAcademicSession, short>>();
            var checkForAcademicSessionInitialisation =
                academicSessionRepository.Find(
                    i => i.Status == Common.Shared.TimeStatus.Current && i.SchoolInfoId == schoolId);
            if (checkForAcademicSessionInitialisation == null)
            {
                return new StudentAssessmentEnvironmentPreparationReturnDto
                {
                    StatusCode = "Failed! You need to first set the 'Academic Session' before you can continue."
                };
            }
            var getAcademicSessionId = checkForAcademicSessionInitialisation.Id;
            if (haveTheyEverDoneStudentsAssessment == null)
            {
                //var range = DateTime.Now.AddMonths(-3);
                var checkRecentEnviromentPreparation =
                    yearlyAndTermlyAssessmentStatusRepository.Where(s => s.Session.SchoolInfoId == schoolId)?
                        .FirstOrDefault();
                if (checkRecentEnviromentPreparation != null)
                {
                    checkRecentEnviromentPreparation.CreatedDate = DateTime.Now;
                    checkRecentEnviromentPreparation.TermStatus = term;
                    checkRecentEnviromentPreparation.TermlyAccessmentStatus = TermlyAccessmentStatus.Current;
                    //checkRecentEnviromentPreparation.SchoolHeadId = hasPrincipalRegistered.Id;
                    checkRecentEnviromentPreparation.SessionId = getAcademicSessionId;
                    yearlyAndTermlyAssessmentStatusRepository.Update(checkRecentEnviromentPreparation);
                    //UnitOfWork.SaveChanges();

                    var checkRecentEnviromentPreparationId = checkRecentEnviromentPreparation.Id;
                    var classStudentsinAllArm = allStudents.GroupBy(s => s.ClassArm.ClassId); //Group by classes
                    foreach (var eachClass in classStudentsinAllArm)
                    {
                        List<SubjectDto> classSubjectCollection;
                        if (eachClass.First().CurrentClass == CurrentClass.Ss1 ||
                            eachClass.First().CurrentClass == CurrentClass.Ss2
                            || eachClass.First().CurrentClass == CurrentClass.Ss3)
                        {
                            var studentDept = eachClass.GroupBy(s => s.ClassCategory);
                            foreach (var eachDept in studentDept)
                            {
                                var classs = eachClass.First().CurrentClass;
                                if (eachDept.First().ClassCategory == Common.Shared.ClassCategoryStatus.Science)
                                {
                                    classSubjectCollection =
                                        SeniorClassesSubjectListCurrent(Common.Shared.ClassCategoryStatus.Science);

                                    var returnString = ProceedFromHereForUpdateSeniorStudent(eachDept,
                                        classSubjectCollection,
                                        checkRecentEnviromentPreparationId, studentAccessmentRepository, schoolId);
                                    if (returnString != "Succeed")
                                    {
                                        return new StudentAssessmentEnvironmentPreparationReturnDto
                                        {
                                            StatusCode = returnString
                                        };
                                    }

                                }
                                else if (eachDept.First().ClassCategory == Common.Shared.ClassCategoryStatus.Commercial)
                                {
                                    classSubjectCollection =
                                        SeniorClassesSubjectListCurrent(Common.Shared.ClassCategoryStatus.Commercial);
                                    var returnString = ProceedFromHereForUpdateSeniorStudent(eachDept,
                                        classSubjectCollection,
                                        checkRecentEnviromentPreparationId, studentAccessmentRepository, schoolId);
                                    if (returnString != "Succeed")
                                    {
                                        return new StudentAssessmentEnvironmentPreparationReturnDto
                                        {
                                            StatusCode = returnString
                                        };
                                    }
                                }
                                else
                                {
                                    classSubjectCollection =
                                        SeniorClassesSubjectListCurrent(Common.Shared.ClassCategoryStatus.Art);
                                    var returnString = ProceedFromHereForUpdateSeniorStudent(eachDept,
                                        classSubjectCollection,
                                        checkRecentEnviromentPreparationId, studentAccessmentRepository, schoolId);
                                    if (returnString != "Succeed")
                                    {
                                        return new StudentAssessmentEnvironmentPreparationReturnDto
                                        {
                                            StatusCode = returnString
                                        };
                                    }
                                }

                            }
                        }
                        else
                        {
                            classSubjectCollection =
                                BasicClassesSubjectListCurrent(eachClass.First().ClassArm.Classs.ClassName);
                            var returnString = ProceedFromHereForUpdateBasicStudents(eachClass, classSubjectCollection,
                                checkRecentEnviromentPreparationId, studentAccessmentRepository, schoolId);
                            if (returnString != "Succeed")
                            {
                                return new StudentAssessmentEnvironmentPreparationReturnDto
                                {
                                    StatusCode = returnString
                                };
                            }
                        }
                    }
                }
                else
                {
                    var assessmentDetails = new TxYearlyAndTermlyStatus
                    {
                        CreatedDate = DateTime.Now,
                        TermStatus = term,
                        TermlyAccessmentStatus = TermlyAccessmentStatus.Current,
                        //SchoolHeadId = hasPrincipalRegistered.Id,
                        SessionId = getAcademicSessionId
                    };
                    yearlyAndTermlyAssessmentStatusRepository.Insert(assessmentDetails);
                    UnitOfWork.SaveChanges();

                    var yearlyAndTermlyAssessmentStatus =
                        yearlyAndTermlyAssessmentStatusRepository.Find(
                            r =>
                                r.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                                r.Session.SchoolInfoId == schoolId);

                    var classStudentsinAllArm = allStudents.GroupBy(s => s.ClassArm.ClassId); //Group by classes
                    foreach (var eachClass in classStudentsinAllArm)
                    {
                        List<SubjectDto> classSubjectCollection;
                        if (eachClass.First().CurrentClass == CurrentClass.Ss1 ||
                            eachClass.First().CurrentClass == CurrentClass.Ss2
                            || eachClass.First().CurrentClass == CurrentClass.Ss2)
                        {
                            var studentDept = eachClass.GroupBy(s => s.ClassCategory);
                            foreach (var eachDept in studentDept)
                            {
                                var classs = eachClass.First().CurrentClass;

                                if (eachDept.First().ClassCategory == Common.Shared.ClassCategoryStatus.Science)
                                {
                                    classSubjectCollection =
                                        SeniorClassesSubjectListCurrent(Common.Shared.ClassCategoryStatus.Science);
                                    var returnString = ProceedFromHereForInsert(eachClass, classSubjectCollection,
                                        yearlyAndTermlyAssessmentStatus.Id, studentAccessmentRepository, schoolId);
                                    if (returnString != "Succeed")
                                    {
                                        return new StudentAssessmentEnvironmentPreparationReturnDto
                                        {
                                            StatusCode = returnString
                                        };
                                    }
                                }
                                else if (eachDept.First().ClassCategory == Common.Shared.ClassCategoryStatus.Commercial)
                                {
                                    classSubjectCollection =
                                        SeniorClassesSubjectListCurrent(Common.Shared.ClassCategoryStatus.Commercial);
                                    var returnString = ProceedFromHereForInsert(eachClass, classSubjectCollection,
                                        yearlyAndTermlyAssessmentStatus.Id, studentAccessmentRepository, schoolId);
                                    if (returnString != "Succeed")
                                    {
                                        return new StudentAssessmentEnvironmentPreparationReturnDto
                                        {
                                            StatusCode = returnString
                                        };
                                    }
                                }
                                else
                                {
                                    classSubjectCollection =
                                        SeniorClassesSubjectListCurrent(Common.Shared.ClassCategoryStatus.Art);
                                    var returnString = ProceedFromHereForInsert(eachClass, classSubjectCollection,
                                        yearlyAndTermlyAssessmentStatus.Id, studentAccessmentRepository, schoolId);
                                    if (returnString != "Succeed")
                                    {
                                        return new StudentAssessmentEnvironmentPreparationReturnDto
                                        {
                                            StatusCode = returnString
                                        };
                                    }
                                }

                            }
                        }
                        else
                        {
                            classSubjectCollection =
                                BasicClassesSubjectListCurrent(eachClass.First().ClassArm.Classs.ClassName);
                            var returnString = ProceedFromHereForInsert(eachClass, classSubjectCollection,
                                yearlyAndTermlyAssessmentStatus.Id, studentAccessmentRepository, schoolId);
                            if (returnString != "Succeed")
                            {
                                return new StudentAssessmentEnvironmentPreparationReturnDto
                                {
                                    StatusCode = returnString
                                };
                            }
                        }
                    }

                }
                return new StudentAssessmentEnvironmentPreparationReturnDto
                {
                    StatusCode = "Succeed",
                    TotalStudentListToPdf = allStudents.Select(r => new TotalStudentListToPdfDto
                    {
                        IdNo = r.IdNo,
                        FullName = r.User.FullName,
                        CurrentClass = r.ClassArm.Classs.ClassName,
                        ClassLabel = r.ClassArm.Classs.ClassLabel,
                        Arm = r.ClassArm.ArmName.ToString(),
                        SchoolInfo = Mapper.Map<SchoolInfoDto>(r.SchoolInfo)
                    }).ToList()
                }; //Succeed
            }
            return new StudentAssessmentEnvironmentPreparationReturnDto
            {
                StatusCode =
                    "No need for Initial Preparation Since they have ever done students' result processing. <br /> Please use the 'New Assessment Handler' button at the left bottom hand side."
            };
        }

        public ReturnSchoolInfoDto GetSchoolInfoAndProprietorCode()
        {
            var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();

            //var schInfo = schoolInfoRepository.Find(u => u.SchoolRegistrationOfficerId == schoolId);
            var proprietorCode = schoolInfoRepository.Find(u => u.SchoolRegistrationOfficerId == schoolId,
                p => p.SchoolRegistrationOfficer.AgentUser);
            if (proprietorCode == null)
            {
                return new ReturnSchoolInfoDto();
            }
            return new ReturnSchoolInfoDto
            {
                SchoolInfo = Mapper.Map<SchoolInfoDto>(proprietorCode),
                ProprietorCode = proprietorCode.SchoolRegistrationOfficer.AgentUser.SchoolRegId
            };
        }

        public SchoolInfoDto GetSchoolInfo()
        {
            var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            //var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            //var schoolRegistrationOfficerRepository = ServiceLocator.Get<IGenericRepository<SchoolRegistrationOfficer>>();
            //var registrationOfficer = schoolRegistrationOfficerRepository.Find(t => t.UserId == userId);
            //if (registrationOfficer != null)
            //{
            var schInfo = schoolInfoRepository.Find(u => u.SchoolRegistrationOfficerId == schoolId);
            if (schInfo == null)
            {
                return new SchoolInfoDto();
            }
            return Mapper.Map<SchoolInfoDto>(schInfo);
            //}
            //return new SchoolInfoDto();
        }

        public bool UpdateSchoolInfo(SchoolInfoDto model)
        {
            var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            //var schoolRegistrationOfficerId = schoolRegistrationOfficerRepository.Find(t => t.UserId == userId).Id;
            var map = Mapper.Map<SchoolTyInfo>(model);
            // map.SchoolRegistrationOfficerId = schoolRegistrationOfficerId;
            schoolInfoRepository.Update(map);
            return true;
        }

        public ClassTeacherAssignmentDto GetTeachersAndClasses()
        {
            var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var teachers =
                teacherRepository.Where(
                    t =>
                        t.TimeStatus == Common.Shared.TimeStatus.Current && t.SchoolId == schoolId &&
                        t.EmployeeStatus == Common.Shared.EmployeeStatus.Teacher, r => r.User)
                    .Select(r => new SelectListItem { Value = r.UserId, Text = r.User.FullName });
            var classes =
                classRepository.Where(s => s.SchoolInfoId == schoolId).OrderBy(x => x.ClassName)
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.ClassLabel.ToString() });
            return new ClassTeacherAssignmentDto { ClassName = classes, TeachersName = teachers };
        }

        public IEnumerable<EmployeeDto> GetAllTeachers()
        {
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            return
                Mapper.Map<IEnumerable<EmployeeDto>>(teacherRepository.Where(u => u.SchoolId == schoolId, u => u.User));
        }

        public IEnumerable<TeacherClassArmAssignmentDto> GetClassTeachers()
        {
            var teacherClassArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var teacherClassArmAssignmentList =
                teacherClassArmAssignmentRepository.Where(u => u.Employee.SchoolId == schoolId, u => u.Employee.User,
                    r => r.Arm.Classs);
            return Mapper.Map<IList<TeacherClassArmAssignmentDto>>(teacherClassArmAssignmentList);
        }

        //public IEnumerable<TeacherSubjectArmAssignmentDto> GetSubjectTeachers()
        //{
        //    var teacherSubjectArmAssignmentRepository = ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
        //    var teacherSubjectArmAssignmentList = teacherSubjectArmAssignmentRepository.GetAll(u => u.ClassTeacher.User, r=>r.Arm, r => r.Subject)?.GroupBy(t => t.TeacherId).Select(r => r.First()).ToList();
        //    return Mapper.Map<IList<TeacherSubjectArmAssignmentDto>>(teacherSubjectArmAssignmentList);
        //}

        public IEnumerable<TeacherSubjectArmAssignmentDto> GetSubjectTeachers()
        {
            var teacherSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var teacherSubjectArmAssignmentList =
                teacherSubjectArmAssignmentRepository.Where(u => u.Employee.SchoolId == schoolId, u => u.Employee.User,
                    r => r.Arm.Classs, r => r.Subject)?
                    .ToList();
            return Mapper.Map<IList<TeacherSubjectArmAssignmentDto>>(teacherSubjectArmAssignmentList);
        }

        public bool CreateSms(string senderNumber, string messageText)
        {
            var number = senderNumber;
            var message = messageText;
            return true;
        }

        //private ProprietorDto ProprietorModel()
        //{
        //    var userId = Thread.CurrentPrincipal.Identity.GetUserId();
        //    var proprietorRepository = ServiceLocator.Get<IGenericRepository<ProprieTytor, byte>>();
        //    var user = proprietorRepository.Find(u => u.UserId == userId);
        //    return Mapper.Map<ProprietorDto>(user);
        //}

        private UserDto CurrentUserModel()
        {
            var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            var user = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>().Find(userId);
            return Mapper.Map<UserDto>(user);
        }

        //public IEnumerable<TeacherClassArmAssignmentDto> GetTeacherAssignedArmAsFormMaster()
        //{
        //    //var teacherModel = Thread.CurrentPrincipal.Identity.GetUserId();
        //    var teacherClassArmAssignmentRepository =
        //        ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
        //    var userId = Thread.CurrentPrincipal.Identity.GetUserId();
        //    var schoolId = GetSchoolId();
        //    var assignedTeacher = teacherClassArmAssignmentRepository.Where(a => a.ClassTeacher.SchoolId == schoolId && a.ClassTeacher.User.Id == userId,
        //        a => a.ClassTeacher.User, a => a.Arm.Classs);
        //    return Mapper.Map<List<TeacherClassArmAssignmentDto>>(assignedTeacher);
        //}

        public AffectiveAndPsychomotorAndTotalAttendanceDto GetAffectiveAndPsychomotorItems()
        {
            var affectiveAndPsychomorRepository =
                ServiceLocator.Get<IGenericRepository<AffectiveXzAndPsychomotor, byte>>();
            var affectiveAndPsychomor = affectiveAndPsychomorRepository.GetAll().ToList();
            var affectiveAndPsychomorDto = Mapper.Map<List<AffectiveAndPsychomotorDto>>(affectiveAndPsychomor);


            var schoolId = "";
            if (Thread.CurrentPrincipal.IsInRole("Registry"))
            {
                schoolId = GetSchoolIdForSchoolRegistrationOfficer(); //
            }
            else if (Thread.CurrentPrincipal.IsInRole("Teacher"))
            {
                schoolId = CurrentTeacherModel().SchoolId; //
            }
            else if (Thread.CurrentPrincipal.IsInRole("Accounting"))
            {
                schoolId = CurrentTeacherModel().SchoolId; //Accounting
            }
            var affectiveAndPsychomotorAndTotalAttendanceReturn = new AffectiveAndPsychomotorAndTotalAttendanceDto();

            var currentYearlyAndTermlyStatus =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>()
                    .Find(
                        x =>
                            x.Session.SchoolInfoId == schoolId &&
                            x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current);

            if (currentYearlyAndTermlyStatus == null)
            {
                affectiveAndPsychomotorAndTotalAttendanceReturn.TotalAttendance = 255;
                affectiveAndPsychomotorAndTotalAttendanceReturn.AffectiveAndPsychomotor = affectiveAndPsychomorDto;
            }
            else
            {
                affectiveAndPsychomotorAndTotalAttendanceReturn.TotalAttendance =
                    currentYearlyAndTermlyStatus.TotalAttendance ?? 0;
                affectiveAndPsychomotorAndTotalAttendanceReturn.AffectiveAndPsychomotor = affectiveAndPsychomorDto;
            }

            return affectiveAndPsychomotorAndTotalAttendanceReturn;
        }

        //public bool CreateAffectiveAndPsychomotorScore(List<CreateAffectiveAndPsychomotorScoreDto> requiredValue)
        //{
        //    //var submit 
        //    //var comments = requiredValue.Find(r=>r.ItemId==0).Score;
        //    var comments = requiredValue.Last().Score;
        //    var splitComment = comments.Split('~').ToList();
        //    var teacherComment = splitComment.First();
        //    var schoolHeadComment = splitComment.Last();
        //    var armId = requiredValue.Last().ItemId;
        //    var studentId = requiredValue[12].Score;
        //    var attendance = requiredValue[12].ItemId;

        //    requiredValue.RemoveAt(12); //Index number is reduced by one here.
        //    requiredValue.RemoveAt(12); //Therefore the last index is 12.
        //    var teacherModel = CurrentTeacherModel();
        //    var schoolId = teacherModel.SchoolId;

        //    var yearlyAndTermlyAssessmentStatusRepository =
        //        ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
        //    var yearlyAndTermlyAssessmentStatusId = yearlyAndTermlyAssessmentStatusRepository.Find(
        //        s => (s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current || s.TermlyAccessmentStatus ==
        //              TermlyAccessmentStatus.ResultProccessing) && s.Session.SchoolInfoId == schoolId).Id;
        //    var formMasterId = teacherModel.Id;
        //    var affectiveAndPsychomotiveRepository =
        //        ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();

        //    var hasAffectiveAndPsychomotorAssessmentValuesSubmitedThisTerm =
        //        affectiveAndPsychomotiveRepository.Find(
        //            s =>
        //                s.StudentId == studentId &&
        //                s.YearlyAndTermlyStatusId == yearlyAndTermlyAssessmentStatusId);
        //    var studentsAttendanceReportRepository =
        //        ServiceLocator.Get<IGenericRepository<StudentsAtteXzndanceAndCommentsReport, int>>();
        //    var getStudentAttendance = studentsAttendanceReportRepository.Find(s => s.StudentId == studentId &&
        //                                                                            s
        //                                                                                .YearlyAndTermlyStatusId ==
        //                                                                            yearlyAndTermlyAssessmentStatusId &&
        //                                                                            s.ClassArmId == armId);

        //    if (hasAffectiveAndPsychomotorAssessmentValuesSubmitedThisTerm != null)
        //    {

        //        foreach (var item in requiredValue)
        //        {
        //            //Since it update, therefore it is only score value that has changeed

        //            var eachAffectiveItemAssessment =
        //                affectiveAndPsychomotiveRepository.Find(
        //                    s =>
        //                        s.StudentId == studentId &&
        //                        s.YearlyAndTermlyStatusId == yearlyAndTermlyAssessmentStatusId
        //                        && s.ItemId == item.ItemId && s.ClassArmId == armId);
        //            var score = byte.Parse(item.Score);
        //            eachAffectiveItemAssessment.Score = score;
        //            affectiveAndPsychomotiveRepository.Update(eachAffectiveItemAssessment);
        //        }
        //        if (getStudentAttendance == null)
        //        {
        //            var model = new StudentsAtteXzndanceAndCommentsReport
        //            {
        //                ClassArmId = armId,
        //                YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
        //                StudentId = studentId,
        //                Attendance = attendance,
        //                SchoolHeadComments = schoolHeadComment,
        //                TeacherComments = teacherComment
        //            };
        //            studentsAttendanceReportRepository.Insert(model);
        //        }
        //        else
        //        {
        //            getStudentAttendance.Attendance = attendance;
        //            getStudentAttendance.TeacherComments = teacherComment;
        //            getStudentAttendance.SchoolHeadComments = schoolHeadComment;
        //            studentsAttendanceReportRepository.Update(getStudentAttendance);
        //        }
        //    }
        //    else
        //    {
        //        foreach (var item in requiredValue)
        //        {
        //            // instanciating the class "AffectiveAndPsychomoTxtorAssessment"
        //            var affectiveAndPsychomotorAssessmentModel =
        //                ServiceLocator.Get<AffectiveAndPsychomoTxtorAssessment>();
        //            affectiveAndPsychomotorAssessmentModel.Id = Guid.NewGuid().ToString();
        //            affectiveAndPsychomotorAssessmentModel.ItemId = item.ItemId;
        //            var score = byte.Parse(item.Score);
        //            affectiveAndPsychomotorAssessmentModel.Score = score;
        //            affectiveAndPsychomotorAssessmentModel.StudentId = studentId;
        //            affectiveAndPsychomotorAssessmentModel.YearlyAndTermlyStatusId =
        //                yearlyAndTermlyAssessmentStatusId;
        //            affectiveAndPsychomotorAssessmentModel.FormMasterId = formMasterId;
        //            affectiveAndPsychomotorAssessmentModel.ClassArmId = armId;
        //            affectiveAndPsychomotiveRepository.Insert(affectiveAndPsychomotorAssessmentModel);
        //        }
        //        var studentsAttendanceReportModel = ServiceLocator.Get<StudentsAtteXzndanceAndCommentsReport>();
        //        studentsAttendanceReportModel.StudentId = studentId;
        //        studentsAttendanceReportModel.YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId;
        //        studentsAttendanceReportModel.Attendance = attendance;
        //        studentsAttendanceReportModel.TeacherComments = teacherComment;
        //        studentsAttendanceReportModel.SchoolHeadComments = schoolHeadComment;
        //        studentsAttendanceReportModel.ClassArmId = armId;
        //        studentsAttendanceReportRepository.Insert(studentsAttendanceReportModel);
        //    }
        //    return true;
        //}

        public bool CreateAffectiveAndPsychomotorScore(List<CreateAffectiveAndPsychomotorScoreDto> requiredValue)
        {
            //var submit 
            //var comments = requiredValue.Find(r=>r.ItemId==0).Score;
            var comments = requiredValue.Last().Score;
            var splitComment = comments.Split('~').ToList();
            var teacherComment = splitComment.First();
            var schoolHeadComment = splitComment.Last();
            var armId = requiredValue.Last().ItemId;
            var studentId = requiredValue[12].Score;
            var attendance = requiredValue[12].ItemId;

            requiredValue.RemoveAt(12); //Index number is reduced by one here.
            requiredValue.RemoveAt(12); //Therefore the last index is 12.
            var teacherModel = CurrentTeacherModel();
            var schoolId = teacherModel.SchoolId;

            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var yearlyAndTermlyAssessmentStatusId = yearlyAndTermlyAssessmentStatusRepository.Find(
                s => (s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current || s.TermlyAccessmentStatus ==
                      TermlyAccessmentStatus.ResultProccessing) && s.Session.SchoolInfoId == schoolId).Id;
            var formMasterId = teacherModel.Id;
            var affectiveAndPsychomotiveRepository =
                ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();

            var hasAffectiveAndPsychomotorAssessmentValuesSubmitedThisTerm =
                affectiveAndPsychomotiveRepository.Find(
                    s =>
                        s.StudentId == studentId &&
                        s.YearlyAndTermlyStatusId == yearlyAndTermlyAssessmentStatusId);
            var studentsAttendanceReportRepository =
                ServiceLocator.Get<IGenericRepository<StudentsAtteXzndanceAndCommentsReport, int>>();
            var getStudentAttendance = studentsAttendanceReportRepository.Find(s => s.StudentId == studentId &&
                                                                                    s
                                                                                        .YearlyAndTermlyStatusId ==
                                                                                    yearlyAndTermlyAssessmentStatusId &&
                                                                                    s.Student.ClassArmId == armId);

            if (hasAffectiveAndPsychomotorAssessmentValuesSubmitedThisTerm != null)
            {

                foreach (var item in requiredValue)
                {
                    //Since it update, therefore it is only score value that has changeed

                    var eachAffectiveItemAssessment =
                        affectiveAndPsychomotiveRepository.Find(
                            s =>
                                s.StudentId == studentId &&
                                s.YearlyAndTermlyStatusId == yearlyAndTermlyAssessmentStatusId
                                && s.ItemId == item.ItemId && s.Student.ClassArmId == armId);
                    var score = byte.Parse(item.Score);

                    if (eachAffectiveItemAssessment != null)
                    {
                        eachAffectiveItemAssessment.Score = score;
                        affectiveAndPsychomotiveRepository.Update(eachAffectiveItemAssessment);
                    }
                    else
                    {
                        var classArmTeacherId =
                            ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>()
                                .Find(t => t.ArmId == armId)
                                .EmployeeId;

                        var model = new AffectiveAndPsychomoTxtorAssessment
                        {
                            Id = Guid.NewGuid().ToString(),
                            ClassArmId = armId,
                            YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                            StudentId = studentId,
                            ItemId = item.ItemId,
                            FormMasterId = classArmTeacherId,
                        };
                        affectiveAndPsychomotiveRepository.Insert(model);
                    }

                }
                if (getStudentAttendance == null)
                {
                    var model = new StudentsAtteXzndanceAndCommentsReport
                    {
                        ClassArmId = armId,
                        YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                        StudentId = studentId,
                        Attendance = attendance,
                        SchoolHeadComments = schoolHeadComment,
                        TeacherComments = teacherComment
                    };
                    studentsAttendanceReportRepository.Insert(model);
                }
                else
                {
                    getStudentAttendance.Attendance = attendance;
                    getStudentAttendance.TeacherComments = teacherComment;
                    getStudentAttendance.SchoolHeadComments = schoolHeadComment;
                    studentsAttendanceReportRepository.Update(getStudentAttendance);
                }
            }
            else
            {
                foreach (var item in requiredValue)
                {
                    // instanciating the class "AffectiveAndPsychomoTxtorAssessment"
                    var affectiveAndPsychomotorAssessmentModel =
                        ServiceLocator.Get<AffectiveAndPsychomoTxtorAssessment>();
                    affectiveAndPsychomotorAssessmentModel.Id = Guid.NewGuid().ToString();
                    affectiveAndPsychomotorAssessmentModel.ItemId = item.ItemId;
                    var score = byte.Parse(item.Score);
                    affectiveAndPsychomotorAssessmentModel.Score = score;
                    affectiveAndPsychomotorAssessmentModel.StudentId = studentId;
                    affectiveAndPsychomotorAssessmentModel.YearlyAndTermlyStatusId =
                        yearlyAndTermlyAssessmentStatusId;
                    affectiveAndPsychomotorAssessmentModel.FormMasterId = formMasterId;
                    affectiveAndPsychomotorAssessmentModel.ClassArmId = armId;
                    affectiveAndPsychomotiveRepository.Insert(affectiveAndPsychomotorAssessmentModel);
                }
                var studentsAttendanceReportModel = ServiceLocator.Get<StudentsAtteXzndanceAndCommentsReport>();
                studentsAttendanceReportModel.StudentId = studentId;
                studentsAttendanceReportModel.YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId;
                studentsAttendanceReportModel.Attendance = attendance;
                studentsAttendanceReportModel.TeacherComments = teacherComment;
                studentsAttendanceReportModel.SchoolHeadComments = schoolHeadComment;
                studentsAttendanceReportModel.ClassArmId = armId;
                studentsAttendanceReportRepository.Insert(studentsAttendanceReportModel);
            }
            return true;
        }

        public bool CreatSchoolHead(SchoolHeadDto model)
        {
            var schoolHeadRepository = ServiceLocator.Get<IGenericRepository<SchoolHeadTx, short>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();

            if (model.HeadStatus == HeadStatus.HeadTeacher)
            {
                var schoolHeadModel =
                    schoolHeadRepository.Find(
                        p =>
                            p.Status == Common.Shared.TimeStatus.Current && p.SchoolInfoId == schoolId &&
                            p.SchoolHeadCategory.HeadStatus == HeadStatus.HeadTeacher);
                if (schoolHeadModel != null)
                {
                    schoolHeadModel.Status = Common.Shared.TimeStatus.Previous;
                    schoolHeadRepository.Update(schoolHeadModel);
                }
            }
            else
            {
                var schoolHeadModel =
                    schoolHeadRepository.Find(
                        p =>
                            p.Status == Common.Shared.TimeStatus.Current && p.SchoolInfoId == schoolId &&
                            p.SchoolHeadCategory.HeadStatus == HeadStatus.Principal);
                if (schoolHeadModel != null)
                {
                    schoolHeadModel.Status = Common.Shared.TimeStatus.Previous;
                    schoolHeadRepository.Update(schoolHeadModel);
                }
            }


            var schoolHeadCategoryId = (byte)(model.HeadStatus == HeadStatus.HeadTeacher ? 1 : 2);
            var sno = (short)(schoolHeadRepository.Count() + 1);
            var mapModel = Mapper.Map<SchoolHeadTx>(model);
            mapModel.Id = sno;
            mapModel.SchoolInfoId = schoolId;
            mapModel.SchoolHeadCategoryId = schoolHeadCategoryId;
            mapModel.Status = Common.Shared.TimeStatus.Current;
            schoolHeadRepository.Insert(mapModel);
            return true;
        }

        public bool CreateResumptionDate(DateTime resumptionDate)
        {
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyAssessmentStatusModel =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    f => f.Session.SchoolInfoId == schoolId &&
                         (f.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                          f.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing));
            if (yearlyAndTermlyAssessmentStatusModel != null)
            {
                yearlyAndTermlyAssessmentStatusModel.NextTermBeginsDate = resumptionDate;
                yearlyAndTermlyAssessmentStatusRepository.Update(yearlyAndTermlyAssessmentStatusModel);
                return true;
            }
            return false;
        }

        public DateTime? GetResumptionDate()
        {
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyAssessmentStatusModel =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    f => f.Session.SchoolInfoId == schoolId && (
                        f.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                        f.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing));

            return yearlyAndTermlyAssessmentStatusModel?.NextTermBeginsDate;
        }

        public IEnumerable<SchoolHeadDto> GetPrincals()
        {
            var principalDetailsTxepository = ServiceLocator.Get<IGenericRepository<SchoolHeadTx, short>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var getCurrentPrincipal = principalDetailsTxepository.Where(s => s.SchoolInfoId == schoolId,
                f => f.SchoolHeadCategory);
            return Mapper.Map<List<SchoolHeadDto>>(getCurrentPrincipal);
        }

        public IEnumerable<SelectListItem> StudentStatus(StudentStatus employee)
        {
            var gender = new List<SelectListItem>
            {
                new SelectListItem {Text = employee.ToString(), Value = ((int) employee).ToString()}
            };
            var enumToList = EnumsAndExtensionMethods.GetEnumToList<StudentStatus>();
            gender.AddRange(from item in enumToList
                            where item != employee
                            select new SelectListItem { Text = item.ToString(), Value = ((int)item).ToString() });
            return gender;
        }

        public IEnumerable<SelectListItem> TimeStatus(TimeStatus employee)
        {
            var gender = new List<SelectListItem>
            {
                new SelectListItem {Text = employee.ToString(), Value = ((int) employee).ToString()}
            };
            var enumToList = EnumsAndExtensionMethods.GetEnumToList<TimeStatus>();
            gender.AddRange(from item in enumToList
                            where item != employee
                            select new SelectListItem { Text = item.ToString(), Value = ((int)item).ToString() });
            return gender;
        }

        public bool UpdateSchoolHeadInfo(SchoolHeadDto model)
        {
            var principalDetailsTxepository = ServiceLocator.Get<IGenericRepository<SchoolHeadTx, short>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();

            if (model.Status == Common.Shared.TimeStatus.Current)
            {
                if (model.HeadStatus == HeadStatus.HeadTeacher)
                {
                    var currentPrincipal =
                        principalDetailsTxepository.Find(
                            c =>
                                c.Status == Common.Shared.TimeStatus.Current && c.SchoolInfoId == schoolId &&
                                c.SchoolHeadCategory.HeadStatus == HeadStatus.HeadTeacher);
                    if (currentPrincipal != null)
                    {
                        currentPrincipal.Status = Common.Shared.TimeStatus.Previous;
                        principalDetailsTxepository.Update(currentPrincipal);
                    }
                }
                else
                {
                    var currentPrincipal =
                        principalDetailsTxepository.Find(
                            c =>
                                c.Status == Common.Shared.TimeStatus.Current && c.SchoolInfoId == schoolId &&
                                c.SchoolHeadCategory.HeadStatus == HeadStatus.Principal);
                    if (currentPrincipal != null)
                    {
                        currentPrincipal.Status = Common.Shared.TimeStatus.Previous;
                        principalDetailsTxepository.Update(currentPrincipal);
                    }
                }
            }
            model.SchoolInfoId = schoolId;
            model.SchoolHeadCategoryId = (byte)(model.HeadStatus == HeadStatus.HeadTeacher ? 1 : 2);
            principalDetailsTxepository.Update(Mapper.Map<SchoolHeadTx>(model));
            return true;
        }

        public byte UpdateTeacherInfo(UserDto model)
        {
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var teacher = teacherRepository.Find(t => t.UserId == model.Id, c=> c.User);
            var teacherClassArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
            var isHeCurrentlyaFormMaster = teacherClassArmAssignmentRepository.Find(t => t.EmployeeId == teacher.Id);
            //You cannot change teacher's status to previous while he still a subject Teacher or a form master
            if (isHeCurrentlyaFormMaster != null && model.TimeStatus == Common.Shared.TimeStatus.Previous)
            {
                return 1;
            }
            var classSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();

            var isHeCurrentlyaSubjectTeacher = classSubjectArmAssignmentRepository.Find(t => t.EmployeeId == teacher.Id);
            if (isHeCurrentlyaSubjectTeacher != null && model.TimeStatus == Common.Shared.TimeStatus.Previous)
            {
                return 2;
            }

            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var hasNewEmailExistAgainstAnotherUser = userRepository.Find(s => s.Email == model.Email)?.Id;
            if (hasNewEmailExistAgainstAnotherUser != null && hasNewEmailExistAgainstAnotherUser != model.Id)
            {
                //Failed! Email exist agaist another record. But in this case we have to update teacher info with duplicate email.
                //Thefore we need to Proceed
            }
            var hasNewUsernameExistAgainstAnotherUser = userRepository.Find(s => s.UserName == model.UserName)?.Id;
            if (hasNewUsernameExistAgainstAnotherUser != null && hasNewUsernameExistAgainstAnotherUser != model.Id)
            {
                return 4; //Failed! Username exist agaist another user.
            }

            var user = teacher.User;
            user.FirstName = model.FirstName;
            user.MiddleName = model.MiddleName;
            user.SurName = model.SurName;
            user.Gender = model.Gender;
            user.Email = model.Email;
            user.PhoneNumber = model.PhoneNumber;
            if (!string.IsNullOrEmpty(model.Password))
            {
                var hasher = new PasswordHasher();
                user.PasswordHash = hasher.HashPassword(model.Password);
            }
            userRepository.Update(user);

            teacher.SignatureImageString = model.SignatureImageString ?? teacher.SignatureImageString;
            teacher.TimeStatus = model.TimeStatus;
            teacher.DateOfBirth = model.DateOfBirth;
            teacher.EmployeeStatus = model.EmployeeStatus;
            teacher.EmployeeIdNo = model.EmployeeIdNo;
            teacherRepository.Update(teacher);
            return 0;
        }

        public UserDto GetTeacherById(int teacherId)
        {
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var model = teacherRepository.Find(t => t.Id == teacherId, t => t.User);
            var status = model.TimeStatus;
            var user = Mapper.Map<UserDto>(model.User);
            user.TimeStatus = status;
            user.SignatureImageString = model.SignatureImageString;
            user.DateOfBirth = model.DateOfBirth;
            user.EmployeeIdNo = model.EmployeeIdNo;
            user.EmployeeStatus = model.EmployeeStatus;
            return user;
        }

        public int DeleteTecher(int teacherId)
        {
            var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
            // var userId = Thread.CurrentPrincipal.Identity.GetUserId();

            var hasThePaymentStructureBeenCreatedForTheEmployee =
                ServiceLocator.Get<IGenericRepository<TxEmployeeIncomeStructure, int>>().Find(teacherId);
            if (hasThePaymentStructureBeenCreatedForTheEmployee != null)
            {
                return 2; //His corresponding payment Structure needs to be deleted first
            }

            var hasHeBeenAssignedAsaSubjectTeacher =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>()
                    .Find(x => x.EmployeeId == teacherId);
            if (hasHeBeenAssignedAsaSubjectTeacher != null)
            {
                return 3; //The teacher needs to be un-assigned as a subject teacher first.
            }
            var hasTeacherEverInputStudentScore = studentAccessmentRepository.Find(s => s.EmployeeId == teacherId);
            if (hasTeacherEverInputStudentScore != null) //if true, there is no need to delete the teacher.
            {
                return 1; //Need to keep employee information
            }

            var hasHeBeenAssignedAsaClassTeacher =
                ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>()
                    .Find(x => x.EmployeeId == teacherId);
            if (hasHeBeenAssignedAsaClassTeacher != null)
            {
                return 4; //The teacher needs to be un-assigned as a class teacher first.
            }

            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var userId = teacherRepository.Find(t => t.Id == teacherId).UserId;
            teacherRepository.Delete(teacherId);
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            userRepository.Delete(userId);
            return 0;
        }

        public bool UpdateSubject(SubjectDto model, SubjectStatus subjectStatus)
        {
            if (model.TimeStatus == Common.Shared.TimeStatus.Previous)
            {
                var teacherSubjectArmAssignmentsRepository =
                    ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
                var teacherSubjectAssignmentList =
                    teacherSubjectArmAssignmentsRepository.Where(z => z.SubjectId == model.Id).ToList();
                foreach (var each in teacherSubjectAssignmentList)
                {
                    teacherSubjectArmAssignmentsRepository.Delete(each);
                }
                var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
                var currentSubjectAssessmenttList =
                    studentAccessmentRepository.Where(
                        z =>
                            z.SubjectId == model.Id &&
                            z.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).ToList();
                foreach (var each in currentSubjectAssessmenttList)
                {
                    studentAccessmentRepository.Delete(each);
                }
            }
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var subectModel = subjectRepository.Find(model.Id);
            subectModel.SubjectName = model.SubjectName;
            subectModel.SubjectCategoryId = model.SubjectCategoryId;
            subectModel.TimeStatus = model.TimeStatus;
            subjectRepository.Update(subectModel);
            return true;
        }

        public bool DeleteSubject(int id)
        {
            var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();

            var subjectAssessmentSample =
                studentAccessmentRepository.Find(
                    s =>
                        s.SubjectId == id &&
                        s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Previous);
            if (subjectAssessmentSample != null)
            {
                return false;
            }
            var subjectAssessmentSampleList =
                studentAccessmentRepository.Where(s => s.SubjectId == id);
            foreach (var eachAssessment in subjectAssessmentSampleList)
            {
                studentAccessmentRepository.Delete(eachAssessment);
            }

            var teacherSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();

            var hasSubjectAssignedToaTeacher =
                teacherSubjectArmAssignmentRepository.Where(s => s.SubjectId == id).ToList();
            foreach (var eachAssignment in hasSubjectAssignedToaTeacher)
            {
                teacherSubjectArmAssignmentRepository.Delete(eachAssignment);
            }
            subjectRepository.Delete(id);
            return true;
            ;
        }

        public bool DeleteStudent(string id)
        {
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var studentAcademicProgressHistory =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
            var previouseStatus = studentAcademicProgressHistory.Find(
                x =>
                    x.StudentId == id &&
                    x.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Previous);
            var academicHistoryStatus = false;
            if (previouseStatus == null)
            {
                var subjectStudentAssignmentRepository =
                    ServiceLocator.Get<IGenericRepository<SubjectStudentTxAssignment, int>>();

                var listAssignedSubject = subjectStudentAssignmentRepository.Where(c => c.StudentId == id).ToList();
                foreach (var eachItem in listAssignedSubject)
                {
                    subjectStudentAssignmentRepository.Delete(eachItem);
                }
                var currentStatus = studentAcademicProgressHistory.Find(
                    x =>
                        x.StudentId == id);


                if (currentStatus != null)
                {
                    studentAcademicProgressHistory.Delete(currentStatus);
                    //Delete current assessment
                    var studentAssessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();

                    var studentCurrentAssessment = studentAssessmentRepository.Where(z => z.StudentId == id).ToList();
                    if (studentCurrentAssessment.Any())
                    {
                        foreach (var eachAssess in studentCurrentAssessment)
                        {
                            studentAssessmentRepository.Delete(eachAssess);
                        }
                    }
                }
                academicHistoryStatus = true;
            }

            if (academicHistoryStatus == false)
            {
                return false;
            }

            var affectiveAndPsychomotorAssessmentRepository =
                ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();
            var studentPsychomorList =
                affectiveAndPsychomotorAssessmentRepository.Where(c => c.StudentId == id).ToList();
            foreach (var each in studentPsychomorList)
            {
                affectiveAndPsychomotorAssessmentRepository.Delete(each);
            }

            var studentsAttendanceAndCommentsReportRepository =
                ServiceLocator.Get<IGenericRepository<StudentsAtteXzndanceAndCommentsReport, int>>();
            var studentCommentAndAttendance =
                studentsAttendanceAndCommentsReportRepository.Find(c => c.StudentId == id);
            if (studentCommentAndAttendance != null)
            {
                studentsAttendanceAndCommentsReportRepository.Delete(studentCommentAndAttendance);
            }

            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var studentAsaUser = studentRepository.Find(s => s.Id == id, o => o.User).User;
            //StudentsAtteXzndanceAndCommentsReports
            studentRepository.Delete(id);
            userRepository.Delete(studentAsaUser);
            return true;
        }

        public IEnumerable<SelectListItem> SubjectsStatus(SubjectStatus status)
        {
            var subjStatus = new List<SelectListItem>
            {
                new SelectListItem {Text = status.ToString(), Value = ((int) status).ToString()}
            };
            var enumToList = EnumsAndExtensionMethods.GetEnumToList<SubjectStatus>();
            subjStatus.AddRange(from item in enumToList
                                where item != status
                                select new SelectListItem { Text = item.ToString(), Value = ((int)item).ToString() });
            return subjStatus;
        }

        public StudentDetailsDto GetStudentById(string id)
        {
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var studentUserModel = studentRepository.Find(a => a.Id == id, r => r.ClassArm, r => r.User).User;
            var studentModel =
                Mapper.Map<StudentDetailsDto>(studentRepository.Find(a => a.Id == id, r => r.ClassArm.Classs));
            studentModel.FirstName = studentUserModel.FirstName;
            studentModel.MiddleName = studentUserModel.MiddleName;
            studentModel.SurName = studentUserModel.SurName;
            studentModel.EmailAddress = studentUserModel.Email;
            studentModel.StudentMobileNo = studentUserModel.PhoneNumber;
            studentModel.Gender = studentUserModel.Gender;
            studentModel.UserId = studentUserModel.Id;
            return studentModel;

        }

        public IEnumerable<SelectListItem> CurrentsClass(CurrentClass currentClass)
        {
            var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var myDropDown = ServiceLocator.GetList<SelectListItem>().ToList();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var classes = classRepository.Where(c => c.SchoolInfoId == schoolId).ToList();
            var currentClasss = classes.Find(r => r.ClassName == currentClass);
            myDropDown.Add(new SelectListItem
            {
                Value = ((int)currentClasss.ClassName).ToString(),
                Text = currentClasss.ClassLabel
            });
            var indexOfCurrentClass = classes.FindIndex(r => r.ClassName == currentClass);
            classes.RemoveAt(indexOfCurrentClass);
            myDropDown.AddRange(
                classes.Select(r => new SelectListItem { Value = ((int)r.ClassName).ToString(), Text = r.ClassLabel }));
            return myDropDown;
        }

        public IEnumerable<SelectListItem> ClasssArms(ArmName armName)
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var armRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var armNames =
                armRepository.Where(r => r.Classs.SchoolInfoId == schoolId)
                    .GroupBy(a => a.ArmName)
                    .Select(r => r.First())
                    .ToList();

            var myDropDown = ServiceLocator.GetList<SelectListItem>().ToList();
            var currentArm = armNames.Find(r => r.ArmName == armName);
            myDropDown.Add(new SelectListItem
            {
                Value = ((int)currentArm.ArmName).ToString(),
                Text = currentArm.ArmName.ToString()
            });
            var indexOfCurrentArm = armNames.FindIndex(r => r.ArmName == armName);
            armNames.RemoveAt(indexOfCurrentArm);
            myDropDown.AddRange(
                armNames.Select(
                    r => new SelectListItem { Value = ((int)r.ArmName).ToString(), Text = r.ArmName.ToString() }));
            return myDropDown;
        }

        public IEnumerable<SelectListItem> StartsClass(string startClass)
        {

            var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var myDropDown = ServiceLocator.GetList<SelectListItem>().ToList();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var classes = classRepository.Where(c => c.SchoolInfoId == schoolId).ToList();
            var currentClasss = classes.Find(r => r.ClassLabel.Trim() == startClass.Trim());
            if (currentClasss != null)
            {
                myDropDown.Add(new SelectListItem
                {
                    Value = currentClasss.ClassLabel,
                    Text = currentClasss.ClassLabel
                });
                var indexOfCurrentClass = classes.FindIndex(r => r.ClassLabel.Trim() == startClass.Trim());
                classes.RemoveAt(indexOfCurrentClass);
            }
            myDropDown.AddRange(classes.Select(r => new SelectListItem { Value = r.ClassLabel, Text = r.ClassLabel }));
            return myDropDown;
        }

        public string UpdateStudentDetails(StudentDetailsDto model, ArmName arm)
        {
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();

            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var armRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var armId =
                armRepository.Find(
                    a =>
                        a.ArmName == arm && a.Classs.ClassName == model.CurrentClass &&
                        a.Classs.SchoolInfoId == schoolId)?.Id;
            if (armId == null)
            {
                return "Failed! You need to first create 'Class Arm' before adding Student to it.";
            }
            model.ClassArmId = (int)armId;
            model.SchoolInfoId = schoolId;
            //model.NetworkType = GetTeachersAndOthersService().GetNetworkType(model.MobileNumber);
            var isThisStudentInHisRegistrationTerm =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>()
                    .Find(x => x.Id == model.AdmisnTermId)
                    ?.Id;

            //var actualClassCategory = GetClassCategoryStatus(model.CurrentClass);
            //model.ClassCategory = actualClassCategory;

            if (isThisStudentInHisRegistrationTerm != null)
            //This is to ensure that the right StudentInitialRegStatus is preserved
            {
                if (model.Status == Common.Shared.StudentStatus.Fresher)
                {
                    model.StudentInitialRegStatus = StudentInitialRegistrationStatus.Fresher;
                }
                else if (model.Status == Common.Shared.StudentStatus.Returning)
                {
                    model.StudentInitialRegStatus = StudentInitialRegistrationStatus.Returning;
                }
            }
            var studentModel = studentRepository.Find(z => z.Id == model.Id, x => x.ClassArm.Classs, f => f.User);

            var schoolInfoRepository =
                ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var schoolObject = schoolInfoRepository.Find(t => t.SchoolRegistrationOfficerId == schoolId);

            if (schoolObject.FeatureImplementationType == null || schoolObject.FeatureImplementationType.Trim().Length == 0)
            {
                //model.Status = model.Status;
            }
            else
            {
                var decryptedImplementaionType = FreePlugIn.Decrypt(schoolObject.FeatureImplementationType, true);
                var implementationType = decryptedImplementaionType.Split(':')[1];
                if (implementationType == "0")//This shows that the implementation type is Result processing  only
                {
                   // model.Status = model.Status;
                }
                else
                {//Accounting or both the accounting and result processing
                    var studentStatus = studentModel.Status;
                    model.Status = studentStatus == Common.Shared.StudentStatus.Previous
                        ? Common.Shared.StudentStatus.Previous
                        : model.Status;
                }
            }
            var schoolSno = GetSchoolSerialId().ToString();
            var studentAdmissnNumber = Regex.Replace(model.IdNo, @"[^0-9a-zA-Z]", "");
            var userName = schoolSno + studentAdmissnNumber;

            var doesUserNameExistAgainsAnotherStudent = DoesThisUserNameExistForStudentUpdate(userName, model.Id);
            if (!doesUserNameExistAgainsAnotherStudent)
            {
                var counter = 0;
                userName = userName + "e";
                while (!doesUserNameExistAgainsAnotherStudent)
                {
                    doesUserNameExistAgainsAnotherStudent = DoesThisUserNameExistForStudentUpdate(userName, model.Id);
                    if (!doesUserNameExistAgainsAnotherStudent)
                    {
                        counter = counter + 1;
                        var newUserName = userName + counter;
                        userName = newUserName;
                    }
                }
            }
            var studentAsaUser = studentModel.User;

            studentAsaUser.UserName = userName;
            studentAsaUser.FirstName = model.FirstName;
            studentAsaUser.MiddleName = model.MiddleName;
            studentAsaUser.SurName = model.SurName;
            studentAsaUser.Gender = model.Gender;
            studentAsaUser.Email = model.EmailAddress;
            studentAsaUser.PhoneNumber = model.StudentMobileNo;

            var studentAssessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
            ClassCategoryStatus actualCurrentCategory;
            if (
                !(studentModel.ClassArm.Classs.ClassName == CurrentClass.Ss1 ||
                  studentModel.ClassArm.Classs.ClassName == CurrentClass.Ss2
                  || studentModel.ClassArm.Classs.ClassName == CurrentClass.Ss3))
            {
                actualCurrentCategory = GetClassCategoryStatus(studentModel.ClassArm.Classs.ClassName);
            }
            else
            {
                actualCurrentCategory = studentModel.ClassCategory;
            }

            var yearlyAndTermlyStatusId = ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>().Find(
                x =>
                    x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                    x.Session.SchoolInfoId == schoolId).Id;

            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();

            if (actualCurrentCategory != model.ClassCategory)
            {
                List<RxSubject> studentSubjectList;
                var hisCurrentAssessmentToDelete = new List<StudenXtAccessment>();
                if (actualCurrentCategory == ClassCategoryStatus.Science ||
                    actualCurrentCategory == ClassCategoryStatus.Art ||
                    actualCurrentCategory == ClassCategoryStatus.Commercial)
                {
                    if (model.ClassCategory == ClassCategoryStatus.Science ||
                        model.ClassCategory == ClassCategoryStatus.Art ||
                        model.ClassCategory == ClassCategoryStatus.Commercial)
                    {
                        #region ifSeniorCategory

                        if (actualCurrentCategory == ClassCategoryStatus.Science)
                        {
                            hisCurrentAssessmentToDelete =
                                studentAssessmentRepository.Where(
                                    s =>
                                        s.StudentId == studentModel.Id &&
                                        (s.Subject.SubjectCategory.SubjectStatus == SubjectStatus.Science ||
                                         s.Subject.SubjectCategory.SubjectStatus != SubjectStatus.AllSeniors)
                                        && s.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId)
                                    .ToList();
                        }
                        else if (actualCurrentCategory == ClassCategoryStatus.Art)
                        {
                            hisCurrentAssessmentToDelete =
                                studentAssessmentRepository.Where(
                                    s =>
                                        s.StudentId == studentModel.Id &&
                                        s.Subject.SubjectCategory.SubjectStatus == SubjectStatus.Art ||
                                        s.Subject.SubjectCategory.SubjectStatus != SubjectStatus.AllSeniors &&
                                        s.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId)
                                    .ToList();
                        }
                        else if (actualCurrentCategory == ClassCategoryStatus.Commercial)
                        {
                            hisCurrentAssessmentToDelete =
                                studentAssessmentRepository.Where(
                                    s =>
                                        s.StudentId == studentModel.Id &&
                                        s.Subject.SubjectCategory.SubjectStatus == SubjectStatus.Commercial ||
                                        s.Subject.SubjectCategory.SubjectStatus != SubjectStatus.AllSeniors
                                        && s.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId)
                                    .ToList();
                        }
                        foreach (var eachAssess in hisCurrentAssessmentToDelete)
                        {
                            studentAssessmentRepository.Delete(eachAssess);
                        }

                        if (model.ClassCategory == ClassCategoryStatus.Science)
                        {
                            studentSubjectList =
                                subjectRepository.Where(
                                    s =>
                                        s.SubjectCategory.SubjectStatus == SubjectStatus.Science &&
                                        s.TimeStatus == Common.Shared.TimeStatus.Current &&
                                        s.SubjectCategory.SchoolId == schoolId).ToList();
                        }
                        else if (model.ClassCategory == ClassCategoryStatus.Art)
                        {
                            studentSubjectList =
                                subjectRepository.Where(
                                    s =>
                                        s.SubjectCategory.SubjectStatus == SubjectStatus.Art &&
                                        s.TimeStatus == Common.Shared.TimeStatus.Current &&
                                        s.SubjectCategory.SchoolId == schoolId).ToList();
                        }
                        else
                        {
                            studentSubjectList =
                                subjectRepository.Where(
                                    s =>
                                        s.SubjectCategory.SubjectStatus == SubjectStatus.Commercial &&
                                        s.TimeStatus == Common.Shared.TimeStatus.Current &&
                                        s.SubjectCategory.SchoolId == schoolId).ToList();
                        }
                        foreach (var eachSubject in studentSubjectList)
                        {
                            var classTeacherId =
                                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>()
                                    .Find(x => x.ArmId == model.ClassArmId && x.SubjectId == eachSubject.Id,
                                        z => z.Employee)
                                    ?.EmployeeId;
                            if (classTeacherId == null)
                            {
                                var noOfStudentInTheSelectedArm =
                                    studentRepository.Count(
                                        x => x.ClassArmId == model.ClassArmId && x.ClassCategory == model.ClassCategory);
                                if (noOfStudentInTheSelectedArm == 0)
                                {

                                }
                                else
                                {
                                    var hisArmModel = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>()
                                        .Find(x => x.Id == model.ClassArmId, z => z.Classs);

                                    return "You need to assign teacher to " + eachSubject.SubjectName +
                                           " in '" + hisArmModel.Classs.ClassLabel + hisArmModel.ArmName + "'";
                                }
                            }
                            else
                            {
                                var noOfStudentInTheSelectedArm =
                                    studentRepository.Count(
                                        x => x.ClassArmId == model.ClassArmId && x.ClassCategory == model.ClassCategory);
                                if (noOfStudentInTheSelectedArm == 1)
                                {
                                    var firstStudentId =
                                        studentRepository.Find(x => x.ClassArmId == model.ClassArmId).Id;
                                    var newAssessment1 = new StudenXtAccessment
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        ClassArmId = model.ClassArmId,
                                        StudentId = firstStudentId,
                                        EmployeeId = (int)classTeacherId,
                                        SubjectId = eachSubject.Id,
                                        LastUpdateDate = DateTime.Now,
                                        Ca1 = 0,
                                        Ca2 = 0,
                                        Ca3 = 0,
                                        Exams = 0,
                                        YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId
                                    };

                                    studentAssessmentRepository.Insert(newAssessment1);
                                }
                                var newAssessment = new StudenXtAccessment
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    ClassArmId = model.ClassArmId,
                                    StudentId = studentModel.Id,
                                    EmployeeId = (int)classTeacherId,
                                    SubjectId = eachSubject.Id,
                                    LastUpdateDate = DateTime.Now,
                                    Ca1 = 0,
                                    Ca2 = 0,
                                    Ca3 = 0,
                                    Exams = 0,
                                    YearlyAndTermlyStatusId = yearlyAndTermlyStatusId
                                };
                                studentAssessmentRepository.Insert(newAssessment);
                            }
                        }

                        #endregion
                    }
                    else //Student is in Basic category (Changes)
                    {
                        #region basicCategory

                        if (model.ClassCategory == ClassCategoryStatus.PlayClass)
                        {
                            hisCurrentAssessmentToDelete =
                                studentAssessmentRepository.Where(
                                    s =>
                                        s.StudentId == studentModel.Id
                                        && s.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId)
                                    .ToList();
                        }
                        else if (model.ClassCategory == ClassCategoryStatus.PreNursery)
                        {
                            hisCurrentAssessmentToDelete =
                                studentAssessmentRepository.Where(
                                    s =>
                                        s.StudentId == studentModel.Id
                                        && s.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId)
                                    .ToList();
                        }
                        else
                        {
                            hisCurrentAssessmentToDelete =
                                studentAssessmentRepository.Where(
                                    s =>
                                        s.StudentId == studentModel.Id &&
                                        (s.Subject.SubjectCategory.SubjectStatus.ToString() ==
                                         studentModel.ClassCategory.ToString() ||
                                         s.Subject.SubjectCategory.SubjectStatus == SubjectStatus.Elective)
                                        && s.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId)
                                    .ToList();
                        }
                        foreach (var eachAssessment in hisCurrentAssessmentToDelete)
                        {
                            studentAssessmentRepository.Delete(eachAssessment);
                        }
                        studentSubjectList =
                            subjectRepository.Where(
                                z =>
                                    z.SubjectCategory.SchoolId == model.SchoolInfoId &&
                                    z.SubjectCategory.SubjectStatus.ToString() == model.ClassCategory.ToString() &&
                                    z.TimeStatus == Common.Shared.TimeStatus.Current).ToList();

                        foreach (var eachSubject in studentSubjectList)
                        {

                            var classTeacherId =
                                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>()
                                    .Find(x => x.ArmId == model.ClassArmId && x.SubjectId == eachSubject.Id,
                                        z => z.Employee)
                                    ?.EmployeeId;
                            if (classTeacherId == null)
                            {
                                var noOfStudentInTheSelectedArm =
                                    studentRepository.Count(x => x.ClassArmId == model.ClassArmId);
                                if (noOfStudentInTheSelectedArm == 0)
                                {

                                }
                                else
                                {
                                    var hisArmModel = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>()
                                        .Find(x => x.Id == model.ClassArmId, z => z.Classs);

                                    return "You need to assign teacher to " + eachSubject.SubjectName +
                                           " in '" + hisArmModel.Classs.ClassLabel + hisArmModel.ArmName + "'";
                                }
                            }
                            else
                            {
                                if (studentRepository.Count(x => x.ClassArmId == model.ClassArmId) == 1)
                                {
                                    var firstStudentId =
                                        studentRepository.Find(x => x.ClassArmId == model.ClassArmId).Id;
                                    var newAssessment1 = new StudenXtAccessment
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        ClassArmId = model.ClassArmId,
                                        StudentId = firstStudentId,
                                        EmployeeId = (int)classTeacherId,
                                        SubjectId = eachSubject.Id,
                                        LastUpdateDate = DateTime.Now,
                                        Ca1 = 0,
                                        Ca2 = 0,
                                        Ca3 = 0,
                                        Exams = 0,
                                        YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId
                                    };
                                    studentAssessmentRepository.Insert(newAssessment1);
                                }
                                var newAssessment = new StudenXtAccessment
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    ClassArmId = model.ClassArmId,
                                    StudentId = studentModel.Id,
                                    EmployeeId = (int)classTeacherId,
                                    SubjectId = eachSubject.Id,
                                    LastUpdateDate = DateTime.Now,
                                    Ca1 = 0,
                                    Ca2 = 0,
                                    Ca3 = 0,
                                    Exams = 0,
                                    YearlyAndTermlyStatusId = yearlyAndTermlyStatusId
                                };
                                studentAssessmentRepository.Insert(newAssessment);
                            }
                        }

                        #endregion
                    }

                }
                else
                {
                    #region ExistingValues //student in Basic class

                    hisCurrentAssessmentToDelete =
                        studentAssessmentRepository.Where(
                            s =>
                                s.StudentId == studentModel.Id
                                && s.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId)
                            .ToList();

                    foreach (var eachAssessment in hisCurrentAssessmentToDelete)
                    {
                        studentAssessmentRepository.Delete(eachAssessment);
                    }

                    if (model.ClassCategory == ClassCategoryStatus.Science ||
                        model.ClassCategory == ClassCategoryStatus.Art ||
                        model.ClassCategory == ClassCategoryStatus.Commercial)
                    {

                        studentSubjectList =
                            subjectRepository.Where(
                                s =>
                                    s.SubjectCategory.SchoolId == model.SchoolInfoId &&
                                    s.TimeStatus == Common.Shared.TimeStatus.Current && (
                                        s.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors ||
                                        s.SubjectCategory.SubjectStatus.ToString() == model.ClassCategory.ToString()))
                                .ToList();

                    }
                    else //Student is in Basic category (Changes values)
                    {
                        studentSubjectList =
                            subjectRepository.Where(
                                s =>
                                    s.SubjectCategory.SchoolId == studentModel.SchoolInfoId &&
                                    s.TimeStatus == Common.Shared.TimeStatus.Current &&
                                    s.SubjectCategory.SubjectStatus.ToString() == model.ClassCategory.ToString())
                                .ToList();
                    }

                    foreach (var eachSubject in studentSubjectList)
                    {
                        var classTeacherId =
                            ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>()
                                .Find(x => x.ArmId == model.ClassArmId && x.SubjectId == eachSubject.Id, z => z.Employee)
                                ?.EmployeeId;
                        if (classTeacherId == null)
                        {
                            var noOfStudentInTheSelectedArm =
                                studentRepository.Count(
                                    x => x.ClassArmId == model.ClassArmId && x.ClassCategory == model.ClassCategory);
                            if (noOfStudentInTheSelectedArm == 0)
                            {

                            }
                            else
                            {
                                var hisArmModel = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>()
                                    .Find(x => x.Id == model.ClassArmId, z => z.Classs);

                                return "You need to assign teacher to " + eachSubject.SubjectName +
                                       " in '" + hisArmModel.Classs.ClassLabel + hisArmModel.ArmName + "'";
                            }
                        }
                        else
                        {
                            if (
                                studentRepository.Count(
                                    x => x.ClassArmId == model.ClassArmId && x.ClassCategory == model.ClassCategory) ==
                                1)
                            {
                                var firstStudentId =
                                    studentRepository.Find(x => x.ClassArmId == model.ClassArmId).Id;
                                var newAssessment1 = new StudenXtAccessment
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    ClassArmId = model.ClassArmId,
                                    StudentId = firstStudentId,
                                    EmployeeId = (int)classTeacherId,
                                    SubjectId = eachSubject.Id,
                                    LastUpdateDate = DateTime.Now,
                                    Ca1 = 0,
                                    Ca2 = 0,
                                    Ca3 = 0,
                                    Exams = 0,
                                    YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId
                                };
                                studentAssessmentRepository.Insert(newAssessment1);
                            }
                            var newAssessment = new StudenXtAccessment
                            {
                                Id = Guid.NewGuid().ToString(),
                                ClassArmId = model.ClassArmId,
                                StudentId = studentModel.Id,
                                EmployeeId = (int)classTeacherId,
                                SubjectId = eachSubject.Id,
                                LastUpdateDate = DateTime.Now,
                                Ca1 = 0,
                                Ca2 = 0,
                                Ca3 = 0,
                                Exams = 0,
                                YearlyAndTermlyStatusId = yearlyAndTermlyStatusId
                            };
                            studentAssessmentRepository.Insert(newAssessment);
                        }
                    }

                    #endregion
                }
            }
            //var studentObj = studentRepository.Find(model.Id);
            //var obj =  Mapper.Map(model, studentObj);



            studentModel.DateOfAdmsn = model.DateOfAdmsn;
            studentModel.DateOfBirth = model.DateOfBirth;
            studentModel.PlaceOfBirth = model.PlaceOfBirth;
            studentModel.CurrentClass = model.CurrentClass;
            studentModel.ClassCategory = model.ClassCategory;
            studentModel.ClassArmId = model.ClassArmId;
            studentModel.IdNo = model.IdNo;
            studentModel.Relationship = model.Relationship;
            studentModel.Occupation = model.Occupation;
            studentModel.Status = model.Status;
            studentModel.AdmisnTermId = model.AdmisnTermId;
            studentModel.HomeNumber = model.HomeNumber;
            studentModel.MobileNumber = model.StudentMobileNo;
            studentModel.EmailAddress = model.EmailAddress;
            studentModel.BloodGroup = model.BloodGroup;
            studentModel.Passport = model.Passport.Length > 2 ? model.Passport : studentModel.Passport;
            studentModel.StartClass = model.StartClass;
            studentModel.Country = model.Country;
            studentModel.DateOfDischarge = model.DateOfDischarge;
            studentModel.GuardianAddress = model.GuardianAddress;
            studentModel.GuardiansName = model.GuardiansName;
            studentModel.Religion = model.Religion;
            studentModel.MailingAddress = model.MailingAddress;
            studentModel.SNo = model.SNo;
            studentModel.TransferedFromWhere = model.TransferedFromWhere;
           // studentModel.
            //studentModel.User.SurName = model.SurName;
            //studentModel.User.FirstName = model.FirstName;
            //studentModel.User.MiddleName = model.MiddleName;
            //studentModel.User.PasswordHash = model.Password;
            //studentModel.User.Email = model.EmailAddress;
            //studentModel.User.Gender = studentAsaUser.Gender;


            studentRepository.Update(studentModel);
            userRepository.Update(studentAsaUser);
            return "";
        }

        public int StudentUpdateValidationChecks(string id, string idNo, CurrentClass currentClass, string startClass,
            ArmName classAarm, string studentEmail, string userId, ClassCategoryStatus classCategory)
        {
            var classArmRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var isClassAlreadyRegistered =
                classRepository.Find(
                    c => c.ClassLabel.Trim().ToLower() == startClass.Trim().ToLower() && c.SchoolInfoId == schoolId);
            if (isClassAlreadyRegistered == null)
            {
                return 1;
            }
            var isClassAlreadyRegisteredForCurrentClass =
                classRepository.Find(c => c.ClassName == currentClass && c.SchoolInfoId == schoolId);
            if (isClassAlreadyRegisteredForCurrentClass == null)
            {
                return 2;
            }
            var isArmAlreadyRegistered =
                classArmRepository.Find(a => a.ArmName == classAarm && a.ClassId == isClassAlreadyRegistered.Id);
            if (isArmAlreadyRegistered == null)
            {
                return 3;
            }
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var hasNewIdExistAgainstAnotherRecord =
                studentRepository.Find(s => s.IdNo == idNo && s.SchoolInfoId == schoolId)?.Id;
            if (hasNewIdExistAgainstAnotherRecord != null && hasNewIdExistAgainstAnotherRecord != id)
            {
                return 4;
            }
            if (currentClass == CurrentClass.Ss1 || currentClass == CurrentClass.Ss2 || currentClass == CurrentClass.Ss3)
            {
                if (
                    !(classCategory == ClassCategoryStatus.Science || classCategory == ClassCategoryStatus.Art ||
                      classCategory == ClassCategoryStatus.Commercial))
                {
                    return 5; //Appropriate class category needs to be selected (Science or Art or Commercial)
                }
            }
            else if (classCategory == ClassCategoryStatus.Science || classCategory == ClassCategoryStatus.Art ||
                     classCategory == ClassCategoryStatus.Commercial)
            {
                if (!(currentClass == CurrentClass.Ss1 || currentClass == CurrentClass.Ss2 ||
                      currentClass == CurrentClass.Ss3))
                {
                    return 5; //Appropriate class category needs to be selected (Science or Art or Commercial)
                }

            }
            else if ((currentClass == CurrentClass.Jss1 || currentClass == CurrentClass.Jss2 ||
                      currentClass == CurrentClass.Jss3) && classCategory != ClassCategoryStatus.Jss)
            {
                return 5;
            }
            else if ((currentClass == CurrentClass.Primary1 || currentClass == CurrentClass.Primary2
                      || currentClass == CurrentClass.Primary3 || currentClass == CurrentClass.Primary4 ||
                      currentClass == CurrentClass.Primary5 || currentClass == CurrentClass.Primary6)
                     && classCategory != ClassCategoryStatus.Primary)
            {
                return 5;
            }
            else if ((currentClass == CurrentClass.Nursery1 || currentClass == CurrentClass.Nursery2)
                     && classCategory != ClassCategoryStatus.Nursery)
            {
                return 5;
            }
            else if ((currentClass == CurrentClass.PreNursery) && classCategory != ClassCategoryStatus.PreNursery)
            {
                return 5;
            }
            else if ((currentClass == CurrentClass.PlayClass) && classCategory != ClassCategoryStatus.PlayClass)
            {
                return 5;
            }
            return 6;
        }

        public bool UpdateClass(ClassDto classModel)
        {
            var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var isTheNewClassNameExistAgainstAnotherRecord =
                classRepository.Find(a => a.ClassName == classModel.ClassName && a.SchoolInfoId == schoolId);

            if (isTheNewClassNameExistAgainstAnotherRecord != null &&
                isTheNewClassNameExistAgainstAnotherRecord.Id != classModel.Id)
            {
                return false;
            }

            var isTheNewClassLabelExistAgainstAnotherRecord =
                classRepository.Find(
                    a =>
                        a.ClassLabel.Trim().ToLower() == classModel.ClassLabel.Trim().ToLower() &&
                        a.SchoolInfoId == schoolId);
            if (isTheNewClassLabelExistAgainstAnotherRecord != null &&
                isTheNewClassLabelExistAgainstAnotherRecord.Id != classModel.Id)
            {
                return false;
            }
            classModel.SchoolInfoId = schoolId;
            classRepository.Update(Mapper.Map<RxClasss>(classModel));
            return true;
        }

        public bool DeleteClass(int id)
        {
            var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var isStudentInAnyOfTheArm =
                studentRepository.Find(s => s.ClassArm.ClassId == id && s.SchoolInfoId == schoolId);
            if (isStudentInAnyOfTheArm == null)
            {
                var classArmRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
                var getArms = classArmRepository.Where(a => a.ClassId == id)?.ToList();
                if (getArms != null)
                {
                    foreach (var eachArm in getArms)
                    {
                        classArmRepository.Delete(eachArm.Id);
                    }
                }
                classRepository.Delete(id);
                return true;
            }
            else
            {
                return false;
            }

        }

        public IEnumerable<SelectListItem> ClasssArm(ArmName armName)
        {
            var clasArm = new List<SelectListItem>
            {
                new SelectListItem {Text = armName.ToString(), Value = ((int) armName).ToString()}
            };
            var enumToList = EnumsAndExtensionMethods.GetEnumToList<ArmName>();
            clasArm.AddRange(from item in enumToList
                             where item != armName
                             select new SelectListItem { Text = item.ToString(), Value = ((int)item).ToString() });
            return clasArm;
        }

        public bool DeleteClassArm(short id)
        {
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var classArmRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var harArmContainStudent = studentRepository.Where(a => a.ClassArmId == id)?.ToList();
            if (harArmContainStudent != null && harArmContainStudent.Any())
            {
                return false;
            }
            classArmRepository.Delete(id);
            return true;
        }

        public bool UpdateClassArm(ClassArmDto classArmModel)
        {
            var classArmRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var isTheNewClassArmExistAgainstAnotherRecord =
                classArmRepository.Find(a => a.ArmName == classArmModel.ArmName && a.ClassId == classArmModel.ClassId);

            if (isTheNewClassArmExistAgainstAnotherRecord != null &&
                isTheNewClassArmExistAgainstAnotherRecord.Id != classArmModel.Id)
            {
                return false;
            }
            classArmRepository.Update(Mapper.Map<AxClassArm>(classArmModel));
            return true;
        }

        public IEnumerable<SelectListItem> Gendar(Gender gender)
        {
            var startClas = new List<SelectListItem>
            {
                new SelectListItem {Text = gender.ToString(), Value = ((int) gender).ToString()}
            };
            var enumToList = EnumsAndExtensionMethods.GetEnumToList<Gender>();
            startClas.AddRange(from item in enumToList
                               where item != gender
                               select new SelectListItem { Text = item.ToString(), Value = ((int)item).ToString() });
            return startClas;
        }

        public SessionsAndTerm GetSessionsAndTerm()
        {
            var academicSessionRepository = ServiceLocator.Get<IGenericRepository<TxAcademicSession, short>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var sessionList = academicSessionRepository.Where(t => t.SchoolInfoId == schoolId)?.ToList();

            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var currentTerm =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    t =>
                        t.TermlyAccessmentStatus == TermlyAccessmentStatus.Current && t.Session.SchoolInfoId == schoolId)
                    ?.TermStatus;

            var sessionAndTerm = ServiceLocator.Get<SessionsAndTerm>();
            sessionAndTerm.Sessions = Mapper.Map<List<AcademicSessionDto>>(sessionList);
            sessionAndTerm.CurrentTerm = currentTerm;

            return sessionAndTerm;
        }

        public bool CreateAcademicSession(AcademicSessionDto model)
        {
            var academicSessionRepository = ServiceLocator.Get<IGenericRepository<TxAcademicSession, short>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var checkUniquenessOfAcademicSession =
                academicSessionRepository.Find(a => a.Session == model.Session && a.SchoolInfoId == schoolId);
            if (checkUniquenessOfAcademicSession != null)
            {
                return false;
            }
            var getCurrentSession =
                academicSessionRepository.Find(
                    p => p.Status == Common.Shared.TimeStatus.Current && p.SchoolInfoId == schoolId);
            if (getCurrentSession != null)
            {
                getCurrentSession.Status = Common.Shared.TimeStatus.Previous;
                academicSessionRepository.Update(getCurrentSession);
                UnitOfWork.SaveChanges();
            }
            //var getSessions = academicSessionRepository.Where(s => s.SchoolInfoId == schoolId);

            //var sNo = academicSessionRepository.GetAll().Max(g=>g.Id);
            //var counter = 0;
            //var txAcademicSessions = getSessions as IList<TxAcademicSession> ?? getSessions.ToList();

            //if (txAcademicSessions.Any())
            //{
            //    var lastRecordId = txAcademicSessions.OrderBy(r => r.Id).Last().Id;
            //    if (lastRecordId != sNo)
            //    {
            //        foreach (var eachSession in txAcademicSessions)
            //        {
            //            counter = counter + 1;
            //            if (counter != eachSession.Id)
            //            {
            //                var academicSessionModel = ServiceLocator.Get<TxAcademicSession>();
            //                academicSessionModel.Id = (short)counter;
            //                academicSessionModel.Status = Common.Shared.TimeStatus.Current;
            //                academicSessionModel.Session = model.Session;
            //                academicSessionModel.SchoolInfoId = schoolId;
            //                academicSessionRepository.Insert(academicSessionModel);
            //            }

            //        }
            //        return true;
            //    }
            //}
            // sNo = (short)(sNo + 1);
            //model.Id = sNo;
            model.SchoolInfoId = schoolId;
            model.Status = Common.Shared.TimeStatus.Current;
            var mapModel = Mapper.Map<TxAcademicSession>(model);
            academicSessionRepository.Insert(mapModel);
            UnitOfWork.SaveChanges();
            return true;
        }

        public bool UpdateAcademicSessionInfo(AcademicSessionDto model)
        {
            var academicSessionTxepository = ServiceLocator.Get<IGenericRepository<TxAcademicSession, short>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var isTheNewAcademicSessionExistAgainstAnotherRecord =
                academicSessionTxepository.Find(a => a.Session == model.Session && a.SchoolInfoId == schoolId);
            if (isTheNewAcademicSessionExistAgainstAnotherRecord != null &&
                isTheNewAcademicSessionExistAgainstAnotherRecord.Id != model.Id)
            {
                return false;
            }
            if (model.Status == Common.Shared.TimeStatus.Current)
            {
                var currentPrincipal =
                    academicSessionTxepository.Find(
                        c => c.Status == Common.Shared.TimeStatus.Current && c.SchoolInfoId == schoolId);
                if (currentPrincipal != null)
                {
                    currentPrincipal.Status = Common.Shared.TimeStatus.Previous;
                    academicSessionTxepository.Update(currentPrincipal);
                }
            }
            model.SchoolInfoId = schoolId;
            academicSessionTxepository.Update(Mapper.Map<TxAcademicSession>(model));
            return true;
        }

        public bool UpdateAffectiveAndPsychomotorScore(List<UpdateAffectiveAndPsychomotorScoreDto> requiredValue)
        {
            var comments = requiredValue[12].AffectiveOrPsychomorId;
            var splitComment = comments.Split('~').ToList();
            var teacherComment = splitComment.First();
            var schoolHeadComment = splitComment.Last();

            var studentId = requiredValue[12].ClassArmId.ToString();
            var attendance = requiredValue[12].ItemId;
            var formMasterId = short.Parse(requiredValue.Last().AffectiveOrPsychomorId);
            requiredValue.RemoveAt(12);
            requiredValue.RemoveAt(12);
            //This index is no longer need since it just to hold:comments, studentId and attendance
            var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var schoolId = teacherRepository.Find(n => n.UserId == userId).SchoolId;
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var yearlyAndTermlyAssessmentStatusId = yearlyAndTermlyAssessmentStatusRepository.Find(
                s => (s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current || s.TermlyAccessmentStatus ==
                      TermlyAccessmentStatus.ResultProccessing) && s.Session.SchoolInfoId == schoolId).Id;
            var affectiveAndPsychomotiveRepository =
                ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();
            var affectiveAndPsychomotorAssessment = ServiceLocator.Get<AffectiveAndPsychomoTxtorAssessment>();
            foreach (var item in requiredValue)
            {
                //var examin = affectiveAndPsychomotiveRepository.Find(r=>r.Id==item.AffectiveOrPsychomorId &&r.SchoolInfoId==schoolId);
                //var id = examin.Id;

                affectiveAndPsychomotorAssessment.Id = item.AffectiveOrPsychomorId;
                affectiveAndPsychomotorAssessment.StudentId = studentId;
                affectiveAndPsychomotorAssessment.ItemId = item.ItemId;
                affectiveAndPsychomotorAssessment.Score = item.Score;
                affectiveAndPsychomotorAssessment.ClassArmId = item.ClassArmId;
                affectiveAndPsychomotorAssessment.YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId;
                affectiveAndPsychomotorAssessment.FormMasterId = formMasterId;
                //affectiveAndPsychomotorAssessment.SchoolInfoId = schoolId;
                affectiveAndPsychomotiveRepository.Update(affectiveAndPsychomotorAssessment);
            }
            var studentsAttendanceReportRepository =
                ServiceLocator.Get<IGenericRepository<StudentsAtteXzndanceAndCommentsReport, int>>();
            var getStudentAttendance = studentsAttendanceReportRepository.Find(s => s.StudentId == studentId &&
                                                                                    s
                                                                                        .YearlyAndTermlyStatusId ==
                                                                                    yearlyAndTermlyAssessmentStatusId);
            var armId = requiredValue.First().ClassArmId;
            getStudentAttendance.ClassArmId = armId;
            getStudentAttendance.Attendance = attendance;
            getStudentAttendance.TeacherComments = teacherComment;
            getStudentAttendance.SchoolHeadComments = schoolHeadComment;
            getStudentAttendance.StudentId = studentId;
            //getStudentAttendance.SchoolInfoId = schoolId;
            studentsAttendanceReportRepository.Update(getStudentAttendance);
            return true;
        }

        public bool CorrectClassLevel()
        {
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyAssessmentStatusId = yearlyAndTermlyAssessmentStatusRepository.Find(
                d =>
                    d.Session.SchoolInfoId == schoolId &&
                    d.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).Id;
            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var allStudents =
                studentDetailRepository.Where(
                    t =>
                        (t.Status == Common.Shared.StudentStatus.Fresher ||
                         t.Status == Common.Shared.StudentStatus.Returning) && t.SchoolInfoId == schoolId,
                    r => r.ClassArm.Classs, u => u.User, g => g.SchoolInfo).ToList();
            var studentAcademicProgressHistoryRepository =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
            foreach (var eachStudent in allStudents)
            {
                if (eachStudent.Status == Common.Shared.StudentStatus.Fresher)
                {
                    eachStudent.Status = Common.Shared.StudentStatus.Returning;
                    studentDetailRepository.Update(eachStudent);
                }
                var currentClass = eachStudent.ClassArm.Classs.ClassName;
                ClassCategoryStatus classCat;
                if (
                    !(currentClass == CurrentClass.Ss1 || currentClass == CurrentClass.Ss2 ||
                      currentClass == CurrentClass.Ss3))
                {
                    classCat = GetClassCategoryStatus(currentClass);
                }
                else
                {
                    classCat = eachStudent.ClassCategory;
                }

                var model = new StudentAcademicTxProgressHistory
                {
                    YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                    StudentId = eachStudent.Id,
                    ClassId = eachStudent.ClassArm.ClassId,
                    ClassCategory = classCat
                };
                studentAcademicProgressHistoryRepository.Insert(model);
                eachStudent.Status = Common.Shared.StudentStatus.Returning;
                studentDetailRepository.Update(eachStudent);
            }
            UnitOfWork.Commit();
            return true;
        }

        public void CorrectPreviosResult()
        {
            var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
            //var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var previosResultsList =
                studentAccessmentRepository.Where(
                    a =>
                        a.YearlyAndTermlyStatusId == 1 && a.Ca1 == 0 && a.Ca2 == 0 && a.Exams == 0).ToList();
            foreach (var each in previosResultsList)
            {
                studentAccessmentRepository.Delete(each);
            }
            UnitOfWork.Commit();
        }

        public PrepareEnvironmentForStudentAssessmentReturnDto PrepareAssessmentEnvironmentForTheWholeStudents(
            byte? requestStatus)
        {
            UnitOfWork.Rollback();
            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var allStudents =
                studentDetailRepository.Where(
                    t =>
                        (t.Status == Common.Shared.StudentStatus.Fresher ||
                         t.Status == Common.Shared.StudentStatus.Returning) && t.SchoolInfoId == schoolId,
                    r => r.ClassArm.Classs, u => u.User, g => g.SchoolInfo).ToList();
            PrepareEnvironmentForStudentAssessmentReturnDto prepareEnvironmentForStudentAssessmentReturn =
                StudentNewAssessmentPreparation(requestStatus, allStudents);
            return prepareEnvironmentForStudentAssessmentReturn;
        }

        private PrepareEnvironmentForStudentAssessmentReturnDto CreateOrUpdateNewAssessment(
            List<TxStudentDetails> allStudents, IGenericRepository<StudenXtAccessment> studentAccessmentRepository,
            string schoolId, int yearlyAndTermlyAssessmentStatusId)
        {
            var classStudentsinAllArm = allStudents.GroupBy(s => s.ClassArm.ClassId); //Group by classes
            var studentsinAllArm = classStudentsinAllArm as IList<IGrouping<int, TxStudentDetails>> ??
                                   classStudentsinAllArm.ToList();
            foreach (var eachClass in studentsinAllArm)
            {
                List<SubjectDto> classSubjectCollection;
                if (eachClass.First().CurrentClass == CurrentClass.Ss1 ||
                    eachClass.First().CurrentClass == CurrentClass.Ss2
                    || eachClass.First().CurrentClass == CurrentClass.Ss3)
                {
                    var studentDept = eachClass.GroupBy(s => s.ClassCategory);
                    foreach (var eachDept in studentDept)
                    {
                        var classs = eachClass.First().CurrentClass;
                        if (eachDept.First().ClassCategory == Common.Shared.ClassCategoryStatus.Science)
                        {
                            classSubjectCollection =
                                SeniorClassesSubjectListCurrent(Common.Shared.ClassCategoryStatus.Science);

                            var returnString = ProceedFromHereForUpdateSeniorStudent(eachDept, classSubjectCollection,
                                yearlyAndTermlyAssessmentStatusId, studentAccessmentRepository, schoolId);
                            if (returnString != "Succeed")
                            {
                                return new PrepareEnvironmentForStudentAssessmentReturnDto
                                {
                                    Message = returnString
                                };
                            }

                        }
                        else if (eachDept.First().ClassCategory == Common.Shared.ClassCategoryStatus.Commercial)
                        {
                            classSubjectCollection =
                                SeniorClassesSubjectListCurrent(Common.Shared.ClassCategoryStatus.Commercial);
                            var returnString = ProceedFromHereForUpdateSeniorStudent(eachDept, classSubjectCollection,
                                yearlyAndTermlyAssessmentStatusId, studentAccessmentRepository, schoolId);
                            if (returnString != "Succeed")
                            {
                                return new PrepareEnvironmentForStudentAssessmentReturnDto
                                {
                                    Message = returnString
                                };
                            }
                        }
                        else if (eachDept.First().ClassCategory == Common.Shared.ClassCategoryStatus.Art)
                        {
                            classSubjectCollection =
                                SeniorClassesSubjectListCurrent(Common.Shared.ClassCategoryStatus.Art);
                            var returnString = ProceedFromHereForUpdateSeniorStudent(eachDept, classSubjectCollection,
                                yearlyAndTermlyAssessmentStatusId, studentAccessmentRepository, schoolId);
                            if (returnString != "Succeed")
                            {
                                return new PrepareEnvironmentForStudentAssessmentReturnDto
                                {
                                    Message = returnString
                                };
                            }
                        }

                    }
                }
                else
                {
                    classSubjectCollection = BasicClassesSubjectListCurrent(eachClass.First().ClassArm.Classs.ClassName);
                    var returnString = ProceedFromHereForUpdateBasicStudents(eachClass, classSubjectCollection,
                        yearlyAndTermlyAssessmentStatusId, studentAccessmentRepository, schoolId);
                    if (returnString != "Succeed")
                    {
                        return new PrepareEnvironmentForStudentAssessmentReturnDto
                        {
                            Message = returnString
                        };
                    }
                }
            }
            //clear student assessment whose status has recently been turned to previous
            var recentPreviousStudent = studentAccessmentRepository.Where(
                t =>
                    t.Student.Status == Common.Shared.StudentStatus.Previous && t.Student.SchoolInfoId == schoolId &&
                    t.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).ToList();
            foreach (var item in recentPreviousStudent)
            {
                if (Math.Abs(item.Ca1 + item.Ca2 + item.Ca3 + item.Exams) < 0.5)
                {
                    studentAccessmentRepository.Delete(item);
                }
            }
            var affectiveAndPsychomotorAssessmentRepository =
                ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();

            var recentPreviousSubject = studentAccessmentRepository.Where(
                t =>
                    t.Subject.TimeStatus == Common.Shared.TimeStatus.Previous &&
                    t.Subject.SubjectCategory.SchoolId == schoolId &&
                    t.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).ToList();
            foreach (var item in recentPreviousSubject)
            {

                if (Math.Abs(item.Ca1 + item.Ca2 + item.Ca3 + item.Exams) < 0.5)
                {
                    studentAccessmentRepository.Delete(item);
                }
            }
            var recentPreviousStudentAffectiveAndPsychomotorAssessmentList =
                affectiveAndPsychomotorAssessmentRepository.Where(
                    x =>
                        x.Student.SchoolInfoId == schoolId &&
                        x.Student.Status == Common.Shared.StudentStatus.Previous &&
                        x.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).ToList();

            var groupByStudentPsycho =
                recentPreviousStudentAffectiveAndPsychomotorAssessmentList.GroupBy(x => x.StudentId);
            foreach (var item in groupByStudentPsycho)
            {
                if (item.Sum(c => c.Score) == 0)
                {
                    var itemToList = item.ToList();
                    foreach (var psychote in itemToList)
                    {
                        affectiveAndPsychomotorAssessmentRepository.Delete(psychote);
                    }
                }
            }
            //End
            return new PrepareEnvironmentForStudentAssessmentReturnDto
            {
                Message = "Success",
                TotalStudentListToPdf = allStudents.Select(r => new TotalStudentListToPdfDto
                {
                    IdNo = r.IdNo,
                    FullName = r.User.FullName,
                    CurrentClass = r.ClassArm.Classs.ClassName,
                    ClassLabel = r.ClassArm.Classs.ClassLabel,
                    Arm = r.ClassArm.ArmName.ToString(),
                    SchoolInfo = Mapper.Map<SchoolInfoDto>(r.SchoolInfo)
                }).ToList()
            };
        }

        private PrepareEnvironmentForStudentAssessmentReturnDto StudentNewAssessmentPreparation(
            byte? requestStatus, List<TxStudentDetails> allStudents)
        {
            var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var isStudentAssessmentEverPerformed =
                studentAccessmentRepository.Count(a => a.Employee.SchoolId == schoolId);
            if (!(isStudentAssessmentEverPerformed > 0))
            {
                return new PrepareEnvironmentForStudentAssessmentReturnDto
                {
                    Message = "You first need to run the first environmental preparation"
                };
            }
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();

            var yearlyAndTermlyAssessmentStatus =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    t => (t.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                          t.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing) &&
                         t.Session.SchoolInfoId == schoolId);

            var academicSessionRepository =
                ServiceLocator.Get<IGenericRepository<TxAcademicSession, short>>();
            var hasCurrentSessionAlreadySet =
                academicSessionRepository.Find(
                    s => s.Status == Common.Shared.TimeStatus.Current && s.SchoolInfoId == schoolId);
            int yearlyAndTermlyAssessmentStatusId = 0;
            var teacherClassArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();

            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            //below condition can only be null after running promotion.
            if (yearlyAndTermlyAssessmentStatus == null)
            {
                // Subsequent environment preparation
                if (hasCurrentSessionAlreadySet == null)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message = "You need to set the current Academic Session to continue."
                    };
                }
                //
                var schoolRepo = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
                var school = schoolRepo.Find(c => c.SchoolRegistrationOfficerId == schoolId);
                school.SystemImplementationType = FreePlugIn.Encrypt(FreePlugIn.GetUniqueSystemId() + ":0", true);
                schoolRepo.Update(school);

                var lastTermsName =
                    studentAccessmentRepository.Where(t => t.Employee.SchoolId == schoolId, a => a.YearlyAndTermlyStatus)
                        .OrderByDescending(g => g.LastUpdateDate)
                        .First()
                        .YearlyAndTermlyStatus.TermStatus;
                var currentTerm = Next(lastTermsName);
                var yearlyAndTermlyAssessmentModel = ServiceLocator.Get<TxYearlyAndTermlyStatus>();

                yearlyAndTermlyAssessmentModel.TermStatus = currentTerm;
                yearlyAndTermlyAssessmentModel.SessionId = hasCurrentSessionAlreadySet.Id;
                yearlyAndTermlyAssessmentModel.CreatedDate = DateTime.Now;
                yearlyAndTermlyAssessmentModel.TermlyAccessmentStatus = TermlyAccessmentStatus.Current;

                var lastYearlyAndTermlyAssessmentStatus =
                    yearlyAndTermlyAssessmentStatusRepository.Where(
                        t => t.Session.SchoolInfoId == schoolId).OrderBy(x => x.Id).Last();

                yearlyAndTermlyAssessmentModel.CasCategory = lastYearlyAndTermlyAssessmentStatus.CasCategory;
                yearlyAndTermlyAssessmentModel.PerCasValue = lastYearlyAndTermlyAssessmentStatus.PerCasValue;
                yearlyAndTermlyAssessmentModel.MinPassMark = lastYearlyAndTermlyAssessmentStatus.MinPassMark;
                yearlyAndTermlyAssessmentModel.PrincipalId = lastYearlyAndTermlyAssessmentStatus.PrincipalId;
                yearlyAndTermlyAssessmentModel.HeadmasterId = lastYearlyAndTermlyAssessmentStatus.HeadmasterId;
                yearlyAndTermlyAssessmentStatusRepository.Insert(yearlyAndTermlyAssessmentModel);

                UnitOfWork.SaveChanges();
                yearlyAndTermlyAssessmentStatusId = yearlyAndTermlyAssessmentStatusRepository.Find(
                    d =>
                        d.Session.SchoolInfoId == schoolId &&
                        d.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).Id;
                //Below closes teachers termly assessment
                var teacherClassArmAssignmentList =
                    teacherClassArmAssignmentRepository.Where(
                        t =>
                            t.Employee.SchoolId == schoolId &&
                            t.ArmTermlyAssessmentStatus == ArmTermlyAssessmentStatus.Completed).ToList();
                foreach (var each in teacherClassArmAssignmentList)
                {
                    each.ArmTermlyAssessmentStatus = ArmTermlyAssessmentStatus.Uncompleted;
                    teacherClassArmAssignmentRepository.Update(each);
                }

                var studentAcademicProgressHistoryRepository =
                    ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
                foreach (var eachStudent in allStudents)
                {
                    if (eachStudent.Status == Common.Shared.StudentStatus.Fresher)
                    {
                        eachStudent.Status = Common.Shared.StudentStatus.Returning;
                        studentDetailRepository.Update(eachStudent);
                    }
                    var currentClass = eachStudent.ClassArm.Classs.ClassName;
                    ClassCategoryStatus classCat;
                    if (
                        !(currentClass == CurrentClass.Ss1 || currentClass == CurrentClass.Ss2 ||
                          currentClass == CurrentClass.Ss3))
                    {
                        classCat = GetClassCategoryStatus(currentClass);
                    }
                    else
                    {
                        classCat = eachStudent.ClassCategory;
                    }
                    var model = new StudentAcademicTxProgressHistory
                    {
                        YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                        StudentId = eachStudent.Id,
                        ClassId = eachStudent.ClassArm.ClassId,
                        ClassCategory = classCat
                    };
                    studentAcademicProgressHistoryRepository.Insert(model);
                }
                UnitOfWork.Commit();

            }
            else
            {
                yearlyAndTermlyAssessmentStatusId = yearlyAndTermlyAssessmentStatus.Id;
                //below confirms that the term has successfully been completed and therefore we need to prepare environment for New Term
                //Below checks if all the Form-Masters have completed of student assessment and return the list of defaulters  

                var isRecordExistInteacherClassArmAssignment =
                    teacherClassArmAssignmentRepository.Where(t => t.Employee.SchoolId == schoolId).Any();
                if (isRecordExistInteacherClassArmAssignment)
                {

                    var teacherClassArmAssignment =
                        teacherClassArmAssignmentRepository.Where(
                            t =>
                                t.Employee.SchoolId == schoolId &&
                                t.ArmTermlyAssessmentStatus == ArmTermlyAssessmentStatus.Uncompleted,
                            t => t.Employee.User, t => t.Arm.Classs)?.ToList();

                    var lastTermsName =
                        studentAccessmentRepository.Where(a => a.Employee.SchoolId == schoolId,
                            a => a.YearlyAndTermlyStatus)
                            .OrderByDescending(g => g.LastUpdateDate)
                            .First()
                            .YearlyAndTermlyStatus.TermStatus;
                    if (teacherClassArmAssignment != null && requestStatus != null)
                    {
                        var returnStuff = teacherClassArmAssignment.Select(r => new UncompletedFormMastersReportDto
                        {
                            FormMasterName =
                                r.Employee.User.FirstName + " " + r.Employee.User.MiddleName + " " +
                                r.Employee.User.SurName,
                            UserName = r.Employee.User.UserName,
                            ClassLabelAndArm = r.Arm.Classs.ClassLabel
                        }).ToList();
                        return new PrepareEnvironmentForStudentAssessmentReturnDto
                        {
                            UncompletedFormMastersReport = returnStuff,
                            Message = returnStuff.Count == 0 ? "Success!" : ""
                        };
                    }
                    if (teacherClassArmAssignment == null || teacherClassArmAssignment.Count == 0)
                    {
                        if (lastTermsName == TermStatus.Third)
                        {
                            return new PrepareEnvironmentForStudentAssessmentReturnDto
                            {
                                Message = "Promotion"
                            };
                        }
                        //Close Last Term by .....
                        yearlyAndTermlyAssessmentStatus.TermlyAccessmentStatus = TermlyAccessmentStatus.Previous;
                        yearlyAndTermlyAssessmentStatusRepository.Update(yearlyAndTermlyAssessmentStatus);

                        var teacherClassArmAssignmentList =
                            teacherClassArmAssignmentRepository.Where(
                                t =>
                                    t.Employee.SchoolId == schoolId &&
                                    t.ArmTermlyAssessmentStatus == ArmTermlyAssessmentStatus.Completed).ToList();
                        foreach (var eachItem in teacherClassArmAssignmentList)
                        {
                            eachItem.ArmTermlyAssessmentStatus = ArmTermlyAssessmentStatus.Uncompleted;
                            teacherClassArmAssignmentRepository.Update(eachItem);
                        }
                        UnitOfWork.Commit();
                        //begin new Term
                        var currentTerm = Next(lastTermsName);
                        var yearlyAndTermlyAssessmentModel = ServiceLocator.Get<TxYearlyAndTermlyStatus>();
                        yearlyAndTermlyAssessmentModel.TermStatus = currentTerm;
                        yearlyAndTermlyAssessmentModel.SessionId = hasCurrentSessionAlreadySet.Id;
                        yearlyAndTermlyAssessmentModel.CreatedDate = DateTime.Now;
                        yearlyAndTermlyAssessmentModel.TermlyAccessmentStatus = TermlyAccessmentStatus.Current;
                        yearlyAndTermlyAssessmentModel.CasCategory = yearlyAndTermlyAssessmentStatus.CasCategory;
                        yearlyAndTermlyAssessmentModel.PerCasValue = yearlyAndTermlyAssessmentStatus.PerCasValue;
                        yearlyAndTermlyAssessmentModel.MinPassMark = yearlyAndTermlyAssessmentStatus.MinPassMark;
                        yearlyAndTermlyAssessmentModel.PrincipalId = yearlyAndTermlyAssessmentStatus.PrincipalId;
                        yearlyAndTermlyAssessmentModel.HeadmasterId = yearlyAndTermlyAssessmentStatus.HeadmasterId;

                        yearlyAndTermlyAssessmentStatusRepository.Insert(yearlyAndTermlyAssessmentModel);
                        UnitOfWork.SaveChanges();
                        yearlyAndTermlyAssessmentStatusId =
                            yearlyAndTermlyAssessmentStatusRepository.Find(
                                d =>
                                    d.Session.SchoolInfoId == schoolId &&
                                    d.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).Id;

                        var studentAcademicProgressHistoryRepository =
                            ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
                        foreach (var eachStudent in allStudents)
                        {
                            if (eachStudent.Status == Common.Shared.StudentStatus.Fresher)
                            {
                                eachStudent.Status = Common.Shared.StudentStatus.Returning;
                                studentDetailRepository.Update(eachStudent);
                            }
                            var currentClass = eachStudent.ClassArm.Classs.ClassName;
                            ClassCategoryStatus classCat;
                            if (
                                !(currentClass == CurrentClass.Ss1 || currentClass == CurrentClass.Ss2 ||
                                  currentClass == CurrentClass.Ss3))
                            {
                                classCat = GetClassCategoryStatus(currentClass);
                            }
                            else
                            {
                                classCat = eachStudent.ClassCategory;
                            }

                            var model = new StudentAcademicTxProgressHistory
                            {
                                YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                                StudentId = eachStudent.Id,
                                ClassId = eachStudent.ClassArm.ClassId,
                                ClassCategory = classCat
                            };
                            studentAcademicProgressHistoryRepository.Insert(model);
                            //eachStudent.Status = Common.Shared.StudentStatus.Returning;
                            //studentDetailRepository.Update(eachStudent);
                        }
                        UnitOfWork.Commit();
                    }
                }
                else
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message =
                            "Failed! For you need to first assign form-masters to thier appropriate class-arm before using this feature."
                    };
                }

                //At this point,Student Id, fullName, and the subject may be captured and return
            }

            PrepareEnvironmentForStudentAssessmentReturnDto createOrUpdateNewAssessment =
                CreateOrUpdateNewAssessment(allStudents, studentAccessmentRepository, schoolId,
                    yearlyAndTermlyAssessmentStatusId);
            return createOrUpdateNewAssessment;
        }

        private string CreateNewTermForNewAssessment2nd(short sessionId,
            TxYearlyAndTermlyStatus yearlyAndTermlyAssessmentStatus,
            IGenericRepository<TxYearlyAndTermlyStatus, int> yearlyAndTermlyAssessmentStatusRepository,
            IGenericRepository<TeacherClassArmTxAssignment, int> teacherClassArmAssignmentRepository, string schoolId,
            TermStatus lastTermsName, IGenericRepository<TxStudentDetails> studentDetailRepository)
        {

            //Close Last Term by .....
            yearlyAndTermlyAssessmentStatus.TermlyAccessmentStatus = TermlyAccessmentStatus.Previous;
            yearlyAndTermlyAssessmentStatusRepository.Update(yearlyAndTermlyAssessmentStatus);

            var teacherClassArmAssignmentList =
                teacherClassArmAssignmentRepository.Where(
                    t =>
                        t.Employee.SchoolId == schoolId &&
                        t.ArmTermlyAssessmentStatus == ArmTermlyAssessmentStatus.Completed).ToList();
            foreach (var eachItem in teacherClassArmAssignmentList)
            {
                eachItem.ArmTermlyAssessmentStatus = ArmTermlyAssessmentStatus.Uncompleted;
                teacherClassArmAssignmentRepository.Update(eachItem);
            }
            UnitOfWork.Commit();
            //begin new Term
            var currentTerm = Next(lastTermsName);
            var yearlyAndTermlyAssessmentModel = ServiceLocator.Get<TxYearlyAndTermlyStatus>();
            yearlyAndTermlyAssessmentModel.TermStatus = currentTerm;
            yearlyAndTermlyAssessmentModel.SessionId = sessionId;
            yearlyAndTermlyAssessmentModel.CreatedDate = DateTime.Now;
            yearlyAndTermlyAssessmentModel.TermlyAccessmentStatus = TermlyAccessmentStatus.Current;
            yearlyAndTermlyAssessmentModel.CasCategory = yearlyAndTermlyAssessmentStatus.CasCategory;
            yearlyAndTermlyAssessmentModel.PerCasValue = yearlyAndTermlyAssessmentStatus.PerCasValue;
            yearlyAndTermlyAssessmentModel.MinPassMark = yearlyAndTermlyAssessmentStatus.MinPassMark;
            yearlyAndTermlyAssessmentModel.PrincipalId = yearlyAndTermlyAssessmentStatus.PrincipalId;
            yearlyAndTermlyAssessmentModel.HeadmasterId = yearlyAndTermlyAssessmentStatus.HeadmasterId;

            yearlyAndTermlyAssessmentStatusRepository.Insert(yearlyAndTermlyAssessmentModel);
            UnitOfWork.SaveChanges();
            var yearlyAndTermlyAssessmentStatusId =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    d =>
                        d.Session.SchoolInfoId == schoolId &&
                        d.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).Id;

            var studentAcademicProgressHistoryRepository =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
            var allStudentList = studentDetailRepository.Where(
                x => x.SchoolInfoId == schoolId && x.Status != Common.Shared.StudentStatus.Previous, z => z.ClassArm.Classs).ToList();

            foreach (var eachStudent in allStudentList)
            {
                if (eachStudent.Status == Common.Shared.StudentStatus.Fresher)
                {
                    eachStudent.Status = Common.Shared.StudentStatus.Returning;
                    studentDetailRepository.Update(eachStudent);
                }
                var currentClass = eachStudent.ClassArm.Classs.ClassName;
                ClassCategoryStatus classCat;
                if (
                    !(currentClass == CurrentClass.Ss1 || currentClass == CurrentClass.Ss2 ||
                      currentClass == CurrentClass.Ss3))
                {
                    classCat = GetClassCategoryStatus(currentClass);
                }
                else
                {
                    classCat = eachStudent.ClassCategory;
                }

                var model = new StudentAcademicTxProgressHistory
                {
                    YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                    StudentId = eachStudent.Id,
                    ClassId = eachStudent.ClassArm.ClassId,
                    ClassCategory = classCat
                };
                studentAcademicProgressHistoryRepository.Insert(model);
                eachStudent.Status = Common.Shared.StudentStatus.Returning;
                studentDetailRepository.Update(eachStudent);
            }
            UnitOfWork.Commit();
            return currentTerm + ":" + yearlyAndTermlyAssessmentStatusId;
        }

        private PrepareEnvironmentForStudentAssessmentReturnDto CreateNewTermForNewAssessmentIst(short sessionId,
            string schoolId,
            IGenericRepository<TxYearlyAndTermlyStatus, int> yearlyAndTermlyAssessmentStatusRepository,
            IGenericRepository<TeacherClassArmTxAssignment, int> teacherClassArmAssignmentRepository,
            List<TxStudentDetails> allStudents,
            IGenericRepository<TxStudentDetails> studentDetailRepository)
        {
            var schoolRepo = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var school = schoolRepo.Find(c => c.SchoolRegistrationOfficerId == schoolId);
            school.SystemImplementationType = FreePlugIn.Encrypt(FreePlugIn.GetUniqueSystemId() + ":0", true);
            schoolRepo.Update(school);

            var lastTermsName =
                yearlyAndTermlyAssessmentStatusRepository.Where(t => t.Session.SchoolInfoId == schoolId)
                    .OrderByDescending(g => g.CreatedDate)
                    .First().TermStatus;


            var currentTerm = Next(lastTermsName);
            var yearlyAndTermlyAssessmentModel = ServiceLocator.Get<TxYearlyAndTermlyStatus>();

            yearlyAndTermlyAssessmentModel.TermStatus = currentTerm;
            yearlyAndTermlyAssessmentModel.SessionId = sessionId;
            yearlyAndTermlyAssessmentModel.CreatedDate = DateTime.Now;
            yearlyAndTermlyAssessmentModel.TermlyAccessmentStatus = TermlyAccessmentStatus.Current;

            var lastYearlyAndTermlyAssessmentStatus =
                yearlyAndTermlyAssessmentStatusRepository.Where(
                    t => t.Session.SchoolInfoId == schoolId).OrderBy(x => x.Id).Last();

            yearlyAndTermlyAssessmentModel.CasCategory = lastYearlyAndTermlyAssessmentStatus.CasCategory;
            yearlyAndTermlyAssessmentModel.PerCasValue = lastYearlyAndTermlyAssessmentStatus.PerCasValue;
            yearlyAndTermlyAssessmentModel.MinPassMark = lastYearlyAndTermlyAssessmentStatus.MinPassMark;
            yearlyAndTermlyAssessmentModel.PrincipalId = lastYearlyAndTermlyAssessmentStatus.PrincipalId;
            yearlyAndTermlyAssessmentModel.HeadmasterId = lastYearlyAndTermlyAssessmentStatus.HeadmasterId;
            yearlyAndTermlyAssessmentStatusRepository.Insert(yearlyAndTermlyAssessmentModel);

            UnitOfWork.SaveChanges();
            var yearlyAndTermlyAssessmentStatusId =
                yearlyAndTermlyAssessmentStatusRepository.Find( //Get termId after new term will have been created
                    d =>
                        d.Session.SchoolInfoId == schoolId &&
                        d.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).Id;

            //Below closes teachers termly assessment
            var teacherClassArmAssignmentList =
                teacherClassArmAssignmentRepository.Where(
                    t =>
                        t.Employee.SchoolId == schoolId &&
                        t.ArmTermlyAssessmentStatus == ArmTermlyAssessmentStatus.Completed).ToList();
            foreach (var each in teacherClassArmAssignmentList)
            {
                each.ArmTermlyAssessmentStatus = ArmTermlyAssessmentStatus.Uncompleted;
                teacherClassArmAssignmentRepository.Update(each);
            }

            var studentAcademicProgressHistoryRepository =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
            foreach (var eachStudent in allStudents)
            {
                if (eachStudent.Status == Common.Shared.StudentStatus.Fresher)
                {
                    eachStudent.Status = Common.Shared.StudentStatus.Returning;
                    studentDetailRepository.Update(eachStudent);
                }
                var currentClass = eachStudent.ClassArm.Classs.ClassName;
                ClassCategoryStatus classCat;
                if (
                    !(currentClass == CurrentClass.Ss1 || currentClass == CurrentClass.Ss2 ||
                      currentClass == CurrentClass.Ss3))
                {
                    classCat = GetClassCategoryStatus(currentClass);
                }
                else
                {
                    classCat = eachStudent.ClassCategory;
                }
                var model = new StudentAcademicTxProgressHistory
                {
                    YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                    StudentId = eachStudent.Id,
                    ClassId = eachStudent.ClassArm.ClassId,
                    ClassCategory = classCat
                };
                studentAcademicProgressHistoryRepository.Insert(model);
                eachStudent.Status = Common.Shared.StudentStatus.Returning;
                studentDetailRepository.Update(eachStudent);
            }
            UnitOfWork.Commit();
            return new PrepareEnvironmentForStudentAssessmentReturnDto
            {
                YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId,
                CurrentTermStatus = currentTerm,
                Message = "Success"
            };
        }

        private PrepareEnvironmentForStudentAssessmentReturnDto CreateEnvironmentForStudentsNewAssessment(
            byte? requestStatus, IGenericRepository<TxStudentDetails> studentDetailRepository,
            List<TxStudentDetails> allStudents)
        {
            var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var isStudentAssessmentEverPerformed =
                studentAccessmentRepository.Count(a => a.Employee.SchoolId == schoolId);
            if (!(isStudentAssessmentEverPerformed > 0))
            {
                var classCategory = allStudents.First().ClassCategory.ToString();
                var subject =
                    ServiceLocator.Get<IGenericRepository<RxSubject, int>>()
                        .Find(
                            x =>
                                x.SubjectCategory.SchoolId == schoolId &&
                                (x.SubjectCategory.SubjectStatus.ToString() == classCategory ||
                                 x.SubjectCategory.SubjectStatus == SubjectStatus.General ||
                                 x.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors));
                if (subject == null)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message = "Failed! Subjects needs to be created for the class(es) you have selected."
                    };
                }
            }
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();

            var yearlyAndTermlyAssessmentStatus =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    t => (t.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                          t.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing) &&
                         t.Session.SchoolInfoId == schoolId);

            var academicSessionRepository =
                ServiceLocator.Get<IGenericRepository<TxAcademicSession, short>>();
            var hasCurrentSessionAlreadySet =
                academicSessionRepository.Find(
                    s => s.Status == Common.Shared.TimeStatus.Current && s.SchoolInfoId == schoolId);
            int yearlyAndTermlyAssessmentStatusId = 0;
            TermStatus? currentTermStatus;
            var teacherClassArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();

            //below condition can only be null after running promotion.
            if (yearlyAndTermlyAssessmentStatus == null)
            {
                // Subsequent environment preparation
                if (hasCurrentSessionAlreadySet == null)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message = "You need to set the current Academic Session to continue."
                    };
                }
                var returnInfo = CreateNewTermForNewAssessmentIst(hasCurrentSessionAlreadySet.Id, schoolId,
                    yearlyAndTermlyAssessmentStatusRepository, teacherClassArmAssignmentRepository, allStudents,
                    studentDetailRepository);
                if (returnInfo.Message != "Success")
                {
                    returnInfo.Message = "Unable to complete the process." + " Note: the current term is '" +
                                         returnInfo.CurrentTermStatus + " term'";
                    return returnInfo;
                }
                yearlyAndTermlyAssessmentStatusId = returnInfo.YearlyAndTermlyStatusId;
                currentTermStatus = returnInfo.CurrentTermStatus;
            }
            else
            {
                yearlyAndTermlyAssessmentStatusId = yearlyAndTermlyAssessmentStatus.Id;
                currentTermStatus = yearlyAndTermlyAssessmentStatus.TermStatus;
                //below confirms that the term has successfully been completed and therefore we need to prepare environment for New Term
                //Below checks if all the Form-Masters have completed of student assessment and return the list of defaulters  

                var isRecordExistInteacherClassArmAssignment =
                    teacherClassArmAssignmentRepository.Where(t => t.Employee.SchoolId == schoolId).Any();
                if (isRecordExistInteacherClassArmAssignment)
                {

                    var teacherClassArmAssignment =
                        teacherClassArmAssignmentRepository.Where(
                            t =>
                                t.Employee.SchoolId == schoolId &&
                                t.ArmTermlyAssessmentStatus == ArmTermlyAssessmentStatus.Uncompleted,
                            t => t.Employee.User, t => t.Arm.Classs).ToList();

                    var lastTermsName = currentTermStatus;
                    if (requestStatus != null)
                    {
                        var returnStuff = teacherClassArmAssignment.Select(r => new UncompletedFormMastersReportDto
                        {
                            FormMasterName =
                                r.Employee.User.FirstName + " " + r.Employee.User.MiddleName + " " +
                                r.Employee.User.SurName,
                            UserName = r.Employee.User.UserName,
                            ClassLabelAndArm = r.Arm.Classs.ClassLabel
                        }).ToList();
                        return new PrepareEnvironmentForStudentAssessmentReturnDto
                        {
                            UncompletedFormMastersReport = returnStuff,
                            Message =
                                returnStuff.Count == 0
                                    ? "Success! Note the current term is '" + currentTermStatus + " term'"
                                    : ""
                        };
                    }
                    if (teacherClassArmAssignment.Count == 0)
                    {
                        if (lastTermsName == TermStatus.Third)
                        {
                            return new PrepareEnvironmentForStudentAssessmentReturnDto
                            {
                                Message = "Promotion"
                            };
                        }
                        var currentTermAndId = CreateNewTermForNewAssessment2nd(hasCurrentSessionAlreadySet.Id,
                            yearlyAndTermlyAssessmentStatus, yearlyAndTermlyAssessmentStatusRepository,
                            teacherClassArmAssignmentRepository, schoolId, (TermStatus)lastTermsName,
                            studentDetailRepository);
                        var currentTermAndIdList = currentTermAndId.Split(':');
                        yearlyAndTermlyAssessmentStatusId = int.Parse(currentTermAndIdList[1]);
                        currentTermStatus = ((TermStatus)Enum.Parse(typeof(TermStatus), currentTermAndIdList[0]));
                    }
                }
                else
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message =
                            "Failed! For you need to first assign form-masters to their appropriate class-arm before using this feature. Note: the current term is '" +
                            currentTermStatus + " term'."
                    };
                }

                //At this point,Student Id, fullName, and the subject may be captured and return
            }


            var classStudentsinAllArm = allStudents.GroupBy(s => s.ClassArm.ClassId); //Group by classes
            var studentsinAllArm = classStudentsinAllArm as IList<IGrouping<int, TxStudentDetails>> ??
                                   classStudentsinAllArm.ToList();
            foreach (var eachClass in studentsinAllArm)
            {
                List<SubjectDto> classSubjectCollection;
                if (eachClass.First().CurrentClass == CurrentClass.Ss1 ||
                    eachClass.First().CurrentClass == CurrentClass.Ss2
                    || eachClass.First().CurrentClass == CurrentClass.Ss3)
                {
                    var studentDept = eachClass.GroupBy(s => s.ClassCategory);
                    foreach (var eachDept in studentDept)
                    {
                        //var classs = eachClass.First().CurrentClass;
                        if (eachDept.First().ClassCategory == Common.Shared.ClassCategoryStatus.Science)
                        {
                            classSubjectCollection =
                                SeniorClassesSubjectListCurrent(Common.Shared.ClassCategoryStatus.Science);

                            var returnString = ProceedFromHereForUpdateSeniorStudent(eachDept, classSubjectCollection,
                                yearlyAndTermlyAssessmentStatusId, studentAccessmentRepository, schoolId);
                            if (returnString != "Succeed")
                            {
                                return new PrepareEnvironmentForStudentAssessmentReturnDto
                                {
                                    Message =
                                        returnString + " Note: the current term is: '" + currentTermStatus + " term'"
                                };
                            }

                        }
                        else if (eachDept.First().ClassCategory == Common.Shared.ClassCategoryStatus.Commercial)
                        {
                            classSubjectCollection =
                                SeniorClassesSubjectListCurrent(Common.Shared.ClassCategoryStatus.Commercial);
                            var returnString = ProceedFromHereForUpdateSeniorStudent(eachDept, classSubjectCollection,
                                yearlyAndTermlyAssessmentStatusId, studentAccessmentRepository, schoolId);
                            if (returnString != "Succeed")
                            {
                                return new PrepareEnvironmentForStudentAssessmentReturnDto
                                {
                                    Message =
                                        returnString + " Note: the current term is: '" + currentTermStatus + " term'"
                                };
                            }
                        }
                        else if (eachDept.First().ClassCategory == Common.Shared.ClassCategoryStatus.Art)
                        {
                            classSubjectCollection =
                                SeniorClassesSubjectListCurrent(Common.Shared.ClassCategoryStatus.Art);
                            var returnString = ProceedFromHereForUpdateSeniorStudent(eachDept, classSubjectCollection,
                                yearlyAndTermlyAssessmentStatusId, studentAccessmentRepository, schoolId);
                            if (returnString != "Succeed")
                            {
                                return new PrepareEnvironmentForStudentAssessmentReturnDto
                                {
                                    Message =
                                        returnString + " Note: the current term is: '" + currentTermStatus + " term'"
                                };
                            }
                        }

                    }
                }
                else
                {
                    classSubjectCollection = BasicClassesSubjectListCurrent(eachClass.First().ClassArm.Classs.ClassName);
                    var returnString = ProceedFromHereForUpdateBasicStudents(eachClass, classSubjectCollection,
                        yearlyAndTermlyAssessmentStatusId, studentAccessmentRepository, schoolId);
                    if (returnString != "Succeed")
                    {
                        return new PrepareEnvironmentForStudentAssessmentReturnDto
                        {
                            Message = returnString + " The current term is: '" + currentTermStatus + " term'"
                        };
                    }
                }
            }
            //clear student assessment whose status has recently been turned to previous
            var recentPreviousStudent = studentAccessmentRepository.Where(
                t =>
                    t.Student.Status == Common.Shared.StudentStatus.Previous && t.Student.SchoolInfoId == schoolId &&
                    t.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).ToList();
            foreach (var item in recentPreviousStudent)
            {
                if (Math.Abs(item.Ca1 + item.Ca2 + item.Ca3 + item.Exams) < 0.5)
                {
                    studentAccessmentRepository.Delete(item);
                }
            }
            var affectiveAndPsychomotorAssessmentRepository =
                ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();

            var recentPreviousSubject = studentAccessmentRepository.Where(
                t =>
                    t.Subject.TimeStatus == Common.Shared.TimeStatus.Previous &&
                    t.Subject.SubjectCategory.SchoolId == schoolId &&
                    t.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).ToList();
            foreach (var item in recentPreviousSubject)
            {

                if (Math.Abs(item.Ca1 + item.Ca2 + item.Ca3 + item.Exams) < 0.5)
                {
                    studentAccessmentRepository.Delete(item);
                }
            }
            var recentPreviousStudentAffectiveAndPsychomotorAssessmentList =
                affectiveAndPsychomotorAssessmentRepository.Where(
                    x =>
                        x.Student.SchoolInfoId == schoolId &&
                        x.Student.Status == Common.Shared.StudentStatus.Previous &&
                        x.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).ToList();

            var groupByStudentPsycho =
                recentPreviousStudentAffectiveAndPsychomotorAssessmentList.GroupBy(x => x.StudentId);
            foreach (var item in groupByStudentPsycho)
            {
                if (item.Sum(c => c.Score) == 0)
                {
                    var itemToList = item.ToList();
                    foreach (var psychote in itemToList)
                    {
                        affectiveAndPsychomotorAssessmentRepository.Delete(psychote);
                    }
                }
            }
            //End

            if (allStudents.Any())
            {
                var returnResult = new PrepareEnvironmentForStudentAssessmentReturnDto
                {
                    Message = "Success. Note: the current term is '" + currentTermStatus + " term'.",
                    TotalStudentListToPdf = new List<TotalStudentListToPdfDto>()
                };
                var totalStudentListToPdf = new TotalStudentListToPdfDto
                {
                    IdNo = "",
                    FullName = "",
                    ClassLabel = "",
                    Arm = "",
                    SchoolInfo = null
                };
                returnResult.TotalStudentListToPdf.Add(totalStudentListToPdf);
                return returnResult;
            }
            else
            {
                return new PrepareEnvironmentForStudentAssessmentReturnDto
                {
                    Message = "Success. Note: the current term is '" + currentTermStatus + " term'.",
                    TotalStudentListToPdf = allStudents.Select(r => new TotalStudentListToPdfDto
                    {
                        IdNo = r.IdNo,
                        FullName = r.User.FullName,
                        CurrentClass = r.ClassArm.Classs.ClassName,
                        ClassLabel = r.ClassArm.Classs.ClassLabel,
                        Arm = r.ClassArm.ArmName.ToString(),
                        SchoolInfo = Mapper.Map<SchoolInfoDto>(r.SchoolInfo)
                    }).ToList()
                };
            }
        }

        public void ClearCurrentAssessmentPreparation()
        {
            var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
            //var yearlyAndTermlyAssessmentStatusRepository =
            //    ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyAssessmentList =
                studentAccessmentRepository.Where(
                    t => t.Student.SchoolInfoId == schoolId && (
                        t.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                        t.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing))
                    .ToList();
            var grByStudentId = yearlyAndTermlyAssessmentList.GroupBy(s => s.StudentId);

            foreach (var each in grByStudentId)
            {
                var subjectList = each.GroupBy(x => x.SubjectId).ToList();
                var eachList = subjectList.Select(c => c.First()).ToList();

                foreach (var each2 in eachList)
                {
                    if (
                        yearlyAndTermlyAssessmentList.Where(
                            c => c.SubjectId == each2.SubjectId && c.StudentId == each2.StudentId).ToList().Count() > 1)
                    {
                        studentAccessmentRepository.Delete(each2);
                    }
                }
            }
            UnitOfWork.Commit();
        }

        public bool VerifyTokenUniqueness(string token)
        {
            if (!string.IsNullOrEmpty(token))
            {
                var tokenEncode = FreePlugIn.Encrypt(token.Trim(), true);
                var isApiTokenExistOnAnotherRecord =
                    ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>()
                        .Find(x => x.SmsApiToken == tokenEncode);
                if (isApiTokenExistOnAnotherRecord != null)
                {
                    return false;
                }
            }
            return true;
        }

        private PrepareEnvironmentForStudentAssessmentReturnDto PerArmStudentProcessing(int classArmId)
        {
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var studentsList =
                studentRepository.Where(
                    s =>
                        (s.Status == Common.Shared.StudentStatus.Fresher ||
                         s.Status == Common.Shared.StudentStatus.Returning) &&
                        s.ClassArmId == classArmId,
                    a => a.ClassArm.Classs).ToList();

            return CreateEnvironmentForStudentsNewAssessment(null,
                studentRepository, studentsList);
        }

        public PrepareEnvironmentForStudentAssessmentReturnDto PrepareAssessmentEnvironmentForAnArmStudents(
            string concatParameters)
        {
            //$("#ArmName").val() + "," + $("#ClassName").val() + "," + $("#Session").val() + "," + $("#assessmentCategory").val() + "," + $("#actionType").val() + "," + $("#psychoAffectDefaultValue").val();
            var itemList = concatParameters.Split(',').ToList();
            var armName = (ArmName)int.Parse(itemList[0]);
            var currentclass = (CurrentClass)int.Parse(itemList[1]);
            //var session = itemList[3];
            var assessmentCategory = itemList[4];
            var actionType = itemList[5];
            byte psychoAffectDefaultValue = 7;
            if (itemList.Count == 7)
            {
                psychoAffectDefaultValue = byte.Parse(itemList[6]);
            }
            var prepareEnvironmentForStudentAssessmentReturn = new PrepareEnvironmentForStudentAssessmentReturnDto();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();

            var classArmId =
                ServiceLocator.Get<IGenericRepository<AxClassArm, int>>().Find(s => s.Classs.SchoolInfoId == schoolId &&
                                                                                    s.ArmName == armName &&
                                                                                    s.Classs.ClassName == currentclass)
                    .Id;

            if (assessmentCategory == "0" && actionType == "0") //Case create current assessment
            {
                prepareEnvironmentForStudentAssessmentReturn = PerArmStudentProcessing(classArmId);
            }
            else if (assessmentCategory == "0" && actionType == "1") //Case delete current assessment
            {
                var yearlyAndTermlyStatusId =
                    ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>().Find(
                        x =>
                            x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                            x.Session.SchoolInfoId == schoolId)?.Id;

                if (yearlyAndTermlyStatusId == null)
                {
                    prepareEnvironmentForStudentAssessmentReturn = PerArmStudentProcessing(classArmId);
                    if (prepareEnvironmentForStudentAssessmentReturn.Message.StartsWith("Success"))
                    {
                        UnitOfWork.Commit();
                        yearlyAndTermlyStatusId =
                            ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>().Find(
                                x =>
                                    x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                                    x.Session.SchoolInfoId == schoolId).Id;
                    }
                    else
                    {
                        return prepareEnvironmentForStudentAssessmentReturn;
                        //Here, promotion has just been done and therefore, there is no current assessment to delete.
                    }
                }

                var studentAssessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
                var currentAssessmentToDelete =
                    studentAssessmentRepository.Where(
                        s =>
                            s.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId &&
                            s.Student.ClassArmId == classArmId, c=> c.Subject)
                        .ToList();

                var byStudentGroup = currentAssessmentToDelete.GroupBy(s => s.StudentId);
                foreach (var each in byStudentGroup)
                {
                    var eachList = each.ToList();
                    var bySubjectGroup = eachList.GroupBy(x => x.Subject.SubjectName);
                    var bySubjectGroupCount = bySubjectGroup.Count();
                    if (bySubjectGroupCount < eachList.Count)
                    {
                        //Means there are duplicate records
                        foreach (var item in bySubjectGroup)
                        {
                            var wrongRec = item.Where(x => x.Ca1 == 0 && x.Ca2 == 0 && x.Ca3 == 0);
                            foreach (var item1 in wrongRec)
                            {
                                studentAssessmentRepository.Delete(item1);
                            }
                        }
                    }
                }
                prepareEnvironmentForStudentAssessmentReturn.Message =
                    "The current assessment of the selected class-arm has successfully been DELETED.";
            }

            if (assessmentCategory == "1" && actionType == "0") //Case create current psychomotor and Affective
            {
                var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
                var studentsList =
                    studentRepository.Where(
                        s =>
                            (s.Status == Common.Shared.StudentStatus.Fresher ||
                             s.Status == Common.Shared.StudentStatus.Returning) &&
                            s.ClassArmId == classArmId,
                        a => a.ClassArm.Classs).ToList();

                var classTeacherId =
                    ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>()
                        .Find(
                            a =>
                                a.ArmId == classArmId)?.EmployeeId;
                if (classTeacherId == null)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message = "Sorry! Teacher needs to be assigned to the selected class-arm(s)."
                    };
                }

                var yearlyAndTermlyStatusId =
                    ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>().Find(
                        x =>
                            x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                            x.Session.SchoolInfoId == schoolId)?.Id;

                if (yearlyAndTermlyStatusId == null)
                {
                    return null; //Here, promotion has just been done and we need to create a new session and term.
                }

                var affectiveAndPsychomotiveRepository =
                    ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();

                var getAffectiveAndPsychomotorItems = GetAffectiveAndPsychomotorItems();
                if (getAffectiveAndPsychomotorItems.TotalAttendance == 255)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message =
                            "Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective."
                    };
                }

                var affectiveAndPsycho = getAffectiveAndPsychomotorItems.AffectiveAndPsychomotor;

                foreach (var each in studentsList)
                {
                    var studentPsychomotiveList = affectiveAndPsychomotiveRepository
                        .Where(
                            r =>
                                r.StudentId == each.Id &&
                                r.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId,
                            r => r.AffectiveAndPsychomotor, r => r.Student).ToList();
                    var armId = studentsList.First().ClassArmId;
                    if (studentPsychomotiveList.Any() && studentPsychomotiveList.Count == 12)
                    {
                        //Do nothing
                    }
                    else if (studentPsychomotiveList.Any() && studentPsychomotiveList.Count < 12)
                    {
                        foreach (var eachItem in affectiveAndPsycho)
                        {
                            var doesValueExist =
                                affectiveAndPsychomotiveRepository.Find(
                                    v =>
                                        v.ItemId == eachItem.Id && v.ClassArmId == armId && v.StudentId == each.Id &&
                                        v.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId);

                            if (doesValueExist == null)
                            {
                                var affectiveXzAndPsychom = new AffectiveAndPsychomoTxtorAssessment
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    ClassArmId = armId,
                                    StudentId = each.Id,
                                    YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId,
                                    ItemId = eachItem.Id,
                                    FormMasterId = (int)classTeacherId,
                                    Score = psychoAffectDefaultValue,
                                };
                                affectiveAndPsychomotiveRepository.Insert(affectiveXzAndPsychom);
                            }
                        }
                    }
                    else if (!studentPsychomotiveList.Any())
                    {

                        foreach (var eachItem in affectiveAndPsycho)
                        {
                            var affectiveXzAndPsychom = new AffectiveAndPsychomoTxtorAssessment
                            {
                                Id = Guid.NewGuid().ToString(),
                                ClassArmId = armId,
                                StudentId = each.Id,
                                YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId,
                                ItemId = eachItem.Id,
                                FormMasterId = (int)classTeacherId,
                                Score = psychoAffectDefaultValue,
                            };
                            affectiveAndPsychomotiveRepository.Insert(affectiveXzAndPsychom);
                        }
                    }
                }
                prepareEnvironmentForStudentAssessmentReturn.Message =
                    "The current psychomotor and Affective of the selected class-arm has successfully been generated.";
            }
            else if (assessmentCategory == "1" && actionType == "1") //Case delete current psychomotor and Affective
            {
                var yearlyAndTermlyStatusId =
                    ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>().Find(
                        x =>
                            x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                            x.Session.SchoolInfoId == schoolId)?.Id;

                if (yearlyAndTermlyStatusId == null)
                {
                    return null;
                    // Here, promotion has just been done and therefore, there is no current assessment to delete.
                }

                var affectiveAndPsychomotorAssessmentRepository =
                    ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();
                var currentAssessmentToDelete =
                    affectiveAndPsychomotorAssessmentRepository.Where(
                        s =>
                            s.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId && s.Student.ClassArmId == classArmId)
                        .ToList();
                foreach (var each2 in currentAssessmentToDelete)
                {
                    affectiveAndPsychomotorAssessmentRepository.Delete(each2);
                }
                prepareEnvironmentForStudentAssessmentReturn.Message =
                    "The current psychomotor and Affective of the selected class-arm has successfully been DELETED.";
            }
            return prepareEnvironmentForStudentAssessmentReturn;
        }

        private PrepareEnvironmentForStudentAssessmentReturnDto PerClassStudentProcessing(int classId)
        {
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var studentsList =
                studentRepository.Where(
                    s =>
                        (s.Status == Common.Shared.StudentStatus.Fresher ||
                         s.Status == Common.Shared.StudentStatus.Returning) &&
                        s.ClassArm.ClassId == classId,
                    a => a.ClassArm.Classs).ToList();
            return CreateEnvironmentForStudentsNewAssessment(null,
                studentRepository, studentsList);
        }

        public PrepareEnvironmentForStudentAssessmentReturnDto PrepareAssessmentEnvironmentForaClassStudents(
            string concatParameters)
        {
            //$("#ArmName").val() + "," + $("#ClassName").val() + "," + $("#Session").val() + "," + $("#assessmentCategory").val() + "," + $("#actionType").val() + "," + $("#psychoAffectDefaultValue").val();
            var itemList = concatParameters.Split(',').ToList();
            var currentclass = (CurrentClass)int.Parse(itemList[0]);
            //var session = itemList[3];
            var assessmentCategory = itemList[3];
            var actionType = itemList[4];
            byte psychoAffectDefaultValue = 7;
            if (itemList.Count == 6)
            {
                psychoAffectDefaultValue = byte.Parse(itemList[5]);
            }

            var prepareEnvironmentForStudentAssessmentReturn = new PrepareEnvironmentForStudentAssessmentReturnDto();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();

            var classId =
                ServiceLocator.Get<IGenericRepository<RxClasss, int>>().Find(s => s.SchoolInfoId == schoolId &&
                                                                                  s.ClassName == currentclass).Id;
            if (assessmentCategory == "0" && actionType == "0") //Case create current assessment
            {
                prepareEnvironmentForStudentAssessmentReturn = PerClassStudentProcessing(classId);
            }
            else if (assessmentCategory == "0" && actionType == "1") //Case delete current assessment
            {
                var yearlyAndTermlyStatusId =
                    ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>().Find(
                        x =>
                            x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                            x.Session.SchoolInfoId == schoolId)?.Id;
                if (yearlyAndTermlyStatusId == null)
                {
                    prepareEnvironmentForStudentAssessmentReturn = PerClassStudentProcessing(classId);
                    if (prepareEnvironmentForStudentAssessmentReturn.Message == "Success")
                    {
                        UnitOfWork.Commit();
                        yearlyAndTermlyStatusId =
                            ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>().Find(
                                x =>
                                    x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                                    x.Session.SchoolInfoId == schoolId).Id;
                    }
                    else
                    {
                        return prepareEnvironmentForStudentAssessmentReturn;
                        //Here, promotion has just been done and therefore, there is no current assessment to delete.
                    }
                }

                var studentAssessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
                var currentAssessmentToDelete =
                    studentAssessmentRepository.Where(
                        s =>
                            s.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId &&
                            s.Student.ClassArm.ClassId == classId, c => c.Subject).ToList();

                var byStudentGroup = currentAssessmentToDelete.GroupBy(x => x.StudentId);

                foreach (var each in byStudentGroup)
                {
                    var eachList = each.ToList();
                    var bySubjectGroup = eachList.GroupBy(x => x.Subject.SubjectName).ToList();
                    if (bySubjectGroup.Count() < eachList.Count)
                    {
                        //Means there are duplicate records
                        foreach (var item in bySubjectGroup)
                        {
                            var wrongRec = item.Where(x => x.Ca1 == 0 && x.Ca2 == 0 && x.Ca3 == 0);
                            foreach (var item1 in wrongRec)
                            {
                                studentAssessmentRepository.Delete(item1);
                            }
                        }
                    }
                }
                prepareEnvironmentForStudentAssessmentReturn.Message =
                    "The current assessment of the selected class has successfully been DELETED.";
            }

            if (assessmentCategory == "1" && actionType == "0") //Case create current psychomotor and Affective
            {
                var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
                var studentsList =
                    studentRepository.Where(
                        s =>
                            (s.Status == Common.Shared.StudentStatus.Fresher ||
                             s.Status == Common.Shared.StudentStatus.Returning) &&
                            s.ClassArm.ClassId == classId,
                        a => a.ClassArm.Classs).ToList();

                var currentClassTeacherList =
                    ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>()
                        .Where(
                            a =>
                                a.Arm.ClassId == classId).ToList();

                var affectiveAndPsychomotiveRepository =
                    ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();

                var getAffectiveAndPsychomotorItems = GetAffectiveAndPsychomotorItems();
                if (getAffectiveAndPsychomotorItems.TotalAttendance == 255)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message =
                            "Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective."
                    };
                }
                var affectiveAndPsycho = getAffectiveAndPsychomotorItems.AffectiveAndPsychomotor;

                var yearlyAndTermlyStatusId =
                    ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>().Find(
                        x =>
                            x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                            x.Session.SchoolInfoId == schoolId)?.Id;
                if (yearlyAndTermlyStatusId == null)
                {
                    return null; //Here, promotion has just been done and we need to create a new session and term.
                }

                foreach (var each in studentsList)
                {
                    var studentPsychomotiveList = affectiveAndPsychomotiveRepository
                        .Where(
                            r =>
                                r.StudentId == each.Id &&
                                r.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId,
                            r => r.AffectiveAndPsychomotor, r => r.Student).ToList();
                    var armId = each.ClassArmId;
                    if (studentPsychomotiveList.Any() && studentPsychomotiveList.Count == 12)
                    {
                        //Do nothing
                    }
                    else if (studentPsychomotiveList.Any() && studentPsychomotiveList.Count < 12)
                    {
                        var classTeacherId = currentClassTeacherList.Find(c => c.ArmId == armId);
                        if (classTeacherId == null)
                        {
                            return new PrepareEnvironmentForStudentAssessmentReturnDto
                            {
                                Message =
                                    "Sorry! Class-teacher has not been assigned to one or more Arm(s) in the selected class."
                            };
                        }
                        foreach (var eachItem in affectiveAndPsycho)
                        {
                            var doesValueExist =
                                affectiveAndPsychomotiveRepository.Find(
                                    v =>
                                        v.ItemId == eachItem.Id && v.ClassArmId == armId && v.StudentId == each.Id &&
                                        v.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId);

                            if (doesValueExist == null)
                            {
                                var affectiveXzAndPsychom = new AffectiveAndPsychomoTxtorAssessment
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    ClassArmId = armId,
                                    StudentId = each.Id,
                                    YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId,
                                    ItemId = eachItem.Id,
                                    FormMasterId = (int)classTeacherId.EmployeeId,
                                    Score = psychoAffectDefaultValue,
                                };
                                affectiveAndPsychomotiveRepository.Insert(affectiveXzAndPsychom);
                            }
                        }
                    }
                    else if (!studentPsychomotiveList.Any())
                    {
                        var classTeacherId = currentClassTeacherList.Find(c => c.ArmId == armId);
                        if (classTeacherId == null)
                        {
                            return new PrepareEnvironmentForStudentAssessmentReturnDto
                            {
                                Message =
                                    "Sorry! Class-teacher has not been assigned to one or more Arm(s) in the selected class."
                            };
                        }
                        foreach (var eachItem in affectiveAndPsycho)
                        {
                            var affectiveXzAndPsychom = new AffectiveAndPsychomoTxtorAssessment
                            {
                                Id = Guid.NewGuid().ToString(),
                                ClassArmId = armId,
                                StudentId = each.Id,
                                YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId,
                                ItemId = eachItem.Id,
                                FormMasterId = (int)classTeacherId.EmployeeId,
                                Score = psychoAffectDefaultValue,
                            };
                            affectiveAndPsychomotiveRepository.Insert(affectiveXzAndPsychom);
                        }
                    }
                }
                prepareEnvironmentForStudentAssessmentReturn.Message =
                    "Success! The current psychomotor and Affective of the selected class has successfully been generated.";
            }
            else if (assessmentCategory == "1" && actionType == "1") //Case delete current psychomotor and Affective
            {
                var yearlyAndTermlyStatusId =
                    ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>().Find(
                        x =>
                            x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                            x.Session.SchoolInfoId == schoolId)?.Id;
                if (yearlyAndTermlyStatusId == null)
                {
                    return null; //Here, promotion has just been done and no psychomotor to delete.
                }
                var affectiveAndPsychomotorAssessmentRepository =
                    ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();
                var currentAssessmentToDelete =
                    affectiveAndPsychomotorAssessmentRepository.Where(
                        s =>
                            s.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId &&
                            s.Student.ClassArm.ClassId == classId).ToList();
                foreach (var each2 in currentAssessmentToDelete)
                {
                    affectiveAndPsychomotorAssessmentRepository.Delete(each2);
                }
                prepareEnvironmentForStudentAssessmentReturn.Message =
                    "The current psychomotor and Affective of the selected class has successfully been DELETED.";
            }
            return prepareEnvironmentForStudentAssessmentReturn;
        }

        public PrepareEnvironmentForStudentAssessmentReturnDto PrepareAssessmentEnvironmentForTheWholeStudent(
            string concatParameters)
        {
            //$("#ArmName").val() + "," + $("#ClassName").val() + "," + $("#Session").val() + "," + $("#assessmentCategory").val() + "," + $("#actionType").val() + "," + $("#psychoAffectDefaultValue").val();
            var itemList = concatParameters.Split(',').ToList();
            //var currentclass = (CurrentClass)int.Parse(itemList[0]);
            //var session = itemList[3];
            var assessmentCategory = itemList[0];
            var actionType = itemList[1];
            byte psychoAffectDefaultValue = 7;
            if (itemList.Count == 3)
            {
                psychoAffectDefaultValue = byte.Parse(itemList[2]);
            }

            var prepareEnvironmentForStudentAssessmentReturn = new PrepareEnvironmentForStudentAssessmentReturnDto();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();


            if (assessmentCategory == "0" && actionType == "0") //Case create current assessment
            {
                var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
                var studentsList =
                    studentRepository.Where(
                        s =>
                            (s.Status == Common.Shared.StudentStatus.Fresher ||
                             s.Status == Common.Shared.StudentStatus.Returning) && s.SchoolInfoId == schoolId,
                        a => a.ClassArm.Classs).ToList();

                var createEnvironmentForStudentsNewAssessment = CreateEnvironmentForStudentsNewAssessment(null,
                    studentRepository, studentsList);
                prepareEnvironmentForStudentAssessmentReturn = createEnvironmentForStudentsNewAssessment;
            }
            else if (assessmentCategory == "0" && actionType == "1") //Case delete current assessment
            {
                var yearlyAndTermlyStatusId =
                    ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>().Find(
                        x =>
                            x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                            x.Session.SchoolInfoId == schoolId)?.Id;
                if (yearlyAndTermlyStatusId == null)
                {
                    return null; //Here, promotion has just been done and no psychomotor to delete.
                }
                var studentAssessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
                var currentAssessmentToDelete =
                    studentAssessmentRepository.Where(
                        s =>
                            s.Student.SchoolInfoId == schoolId &&
                            s.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId, c => c.Subject).ToList();

                var byStudentGroup = currentAssessmentToDelete.GroupBy(x => x.StudentId);
                foreach (var each in byStudentGroup)
                {
                    var eachList = each.ToList();
                    var bySubjectGroup = eachList.GroupBy(x => x.Subject.SubjectName).ToList();
                    if (bySubjectGroup.Count() < eachList.Count)
                    {
                        //Means there are duplicate records
                        foreach (var item in bySubjectGroup)
                        {
                            var wrongRec = item.Where(x => x.Ca1 == 0 && x.Ca2 == 0 && x.Ca3 == 0);
                            foreach (var item1 in wrongRec)
                            {
                                studentAssessmentRepository.Delete(item1);
                            }
                        }
                    }
                }
                prepareEnvironmentForStudentAssessmentReturn.Message =
                    "The current assessment of the selected class has successfully been DELETED.";
            }

            if (assessmentCategory == "1" && actionType == "0") //Case create current psychomotor and Affective
            {
                var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
                var studentsList =
                    studentRepository.Where(
                        s =>
                            (s.Status == Common.Shared.StudentStatus.Fresher ||
                             s.Status == Common.Shared.StudentStatus.Returning) && s.SchoolInfoId == schoolId,
                        a => a.ClassArm.Classs).ToList();

                var schoolClassTeacherList =
                    ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>()
                        .Where(
                            a =>
                                a.Arm.Classs.SchoolInfoId == schoolId).ToList();

                var affectiveAndPsychomotiveRepository =
                    ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();

                var getAffectiveAndPsychomotorItems = GetAffectiveAndPsychomotorItems();
                if (getAffectiveAndPsychomotorItems.TotalAttendance == 255)
                {
                    return new PrepareEnvironmentForStudentAssessmentReturnDto
                    {
                        Message =
                            "Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective."
                    };
                }
                var affectiveAndPsycho = getAffectiveAndPsychomotorItems.AffectiveAndPsychomotor;

                var yearlyAndTermlyStatusId =
                    ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>().Find(
                        x =>
                            x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                            x.Session.SchoolInfoId == schoolId)?.Id;
                if (yearlyAndTermlyStatusId == null)
                {
                    return null;
                    //Here, promotion has just been done and therefore there is need to first create a new session/term in order to continue.
                }

                foreach (var each in studentsList)
                {
                    var studentPsychomotiveList = affectiveAndPsychomotiveRepository
                        .Where(
                            r =>
                                r.StudentId == each.Id &&
                                r.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId,
                            r => r.AffectiveAndPsychomotor, r => r.Student).ToList();
                    var armId = each.ClassArmId;
                    if (studentPsychomotiveList.Any() && studentPsychomotiveList.Count == 12)
                    {
                        //Do nothing
                    }
                    else if (studentPsychomotiveList.Any() && studentPsychomotiveList.Count < 12)
                    {
                        var classTeacher = schoolClassTeacherList.Find(c => c.ArmId == armId);
                        if (classTeacher == null)
                        {
                            return new PrepareEnvironmentForStudentAssessmentReturnDto
                            {
                                Message =
                                    "Sorry! Class-teacher has not been assigned to one or more Class-Arm(s) of the school."
                            };
                        }
                        foreach (var eachItem in affectiveAndPsycho)
                        {
                            var doesValueExist =
                                affectiveAndPsychomotiveRepository.Find(
                                    v =>
                                        v.ItemId == eachItem.Id && v.ClassArmId == armId && v.StudentId == each.Id &&
                                        v.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId);

                            if (doesValueExist == null)
                            {
                                var affectiveXzAndPsychom = new AffectiveAndPsychomoTxtorAssessment
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    ClassArmId = armId,
                                    StudentId = each.Id,
                                    YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId,
                                    ItemId = eachItem.Id,
                                    FormMasterId = (int)classTeacher.EmployeeId,
                                    Score = psychoAffectDefaultValue,
                                };
                                affectiveAndPsychomotiveRepository.Insert(affectiveXzAndPsychom);
                            }
                        }
                    }
                    else if (!studentPsychomotiveList.Any())
                    {
                        var classTeacher = schoolClassTeacherList.Find(c => c.ArmId == armId);
                        if (classTeacher == null)
                        {
                            return new PrepareEnvironmentForStudentAssessmentReturnDto
                            {
                                Message =
                                    "Sorry! Class-teacher has not been assigned to one or more Class-Arm(s) of the school."
                            };
                        }
                        foreach (var eachItem in affectiveAndPsycho)
                        {
                            var affectiveXzAndPsychom = new AffectiveAndPsychomoTxtorAssessment
                            {
                                Id = Guid.NewGuid().ToString(),
                                ClassArmId = armId,
                                StudentId = each.Id,
                                YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId,
                                ItemId = eachItem.Id,
                                FormMasterId = (int)classTeacher.EmployeeId,
                                Score = psychoAffectDefaultValue,
                            };
                            affectiveAndPsychomotiveRepository.Insert(affectiveXzAndPsychom);
                        }
                    }
                }
                prepareEnvironmentForStudentAssessmentReturn.Message =
                    "Success! The current psychomotor and Affective of the selected class(es) or class-arm(s) has successfully been generated.";
            }
            else if (assessmentCategory == "1" && actionType == "1") //Case delete current psychomotor and Affective
            {
                var yearlyAndTermlyStatusId =
                    ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>().Find(
                        x =>
                            x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                            x.Session.SchoolInfoId == schoolId)?.Id;
                if (yearlyAndTermlyStatusId == null)
                {
                    return null; //Here, promotion has just been done and therefore there is nothing to delete.
                }

                var affectiveAndPsychomotorAssessmentRepository =
                    ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();
                var currentAssessmentToDelete =
                    affectiveAndPsychomotorAssessmentRepository.Where(
                        s =>
                            s.Student.SchoolInfoId == schoolId &&
                            s.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId).ToList();
                foreach (var each2 in currentAssessmentToDelete)
                {
                    affectiveAndPsychomotorAssessmentRepository.Delete(each2);
                }
                prepareEnvironmentForStudentAssessmentReturn.Message =
                    "The current psychomotor and Affective of the selected class has successfully been DELETED.";
            }
            return prepareEnvironmentForStudentAssessmentReturn;
        }
        public StudentDetailsDto CurrentStudentModel()
        {
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            var currentUserModel = teacherRepository.Find(c => c.UserId == userId, r => r.SchoolInfo);
            return Mapper.Map<StudentDetailsDto>(currentUserModel);
        }

        public EmployeeDto CurrentTeacherModel()
        {
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            var currentUserModel = teacherRepository.Find(c => c.UserId == userId, r => r.SchoolInfo);
            return Mapper.Map<EmployeeDto>(currentUserModel);
        }

        public IEnumerable<StudentsAccessmentDto> GetArmStudentAssessmentByArmId(int armId)
        {
            var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var schoolId = CurrentTeacherModel().SchoolId;
            var yearlyAndTermlyAssessmentStatus =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    t => t.Session.SchoolInfoId == schoolId && (
                        t.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                        t.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing));
            if (yearlyAndTermlyAssessmentStatus != null)
            {

                List<StudenXtAccessment> studentAccessment;
                if (yearlyAndTermlyAssessmentStatus.TermStatus == TermStatus.Third)
                {
                    studentAccessment = studentAccessmentRepository.Where(
                        s =>
                            s.Student.ClassArmId == armId &&
                            s.YearlyAndTermlyStatus.SessionId == yearlyAndTermlyAssessmentStatus.SessionId,
                        s => s.Student.User).ToList();
                }
                else
                {
                    studentAccessment = studentAccessmentRepository.Where(
                        s =>
                            s.Student.ClassArmId == armId &&
                            s.YearlyAndTermlyStatusId == yearlyAndTermlyAssessmentStatus.Id, s => s.Student.User)
                        .ToList();
                }
                return Mapper.Map<List<StudentsAccessmentDto>>(studentAccessment);
            }
            return new List<StudentsAccessmentDto>();

        }

        public bool UndoRecentStudentsPromotion3rd()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var studentAcademicProgressHistoryRepository =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();

            var studentAcademicProgressHistoryList = studentAcademicProgressHistoryRepository.Where(
                    s =>
                        s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        s.Classs.SchoolInfoId == schoolId).ToList();

            foreach (var item in studentAcademicProgressHistoryList)
            {
                studentAcademicProgressHistoryRepository.Delete(item);
            }

            var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
            var currentStudentAssessmentList =
                   studentAccessmentRepository.Where(
                       s =>
                           s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                           s.Student.SchoolInfoId == schoolId).ToList(); //.Sum(t => t.Ca1 + t.Ca2 + t.Ca3 + t.Exams);

            foreach (var item in currentStudentAssessmentList)
            {
                studentAccessmentRepository.Delete(item);
            }


            var affectiveAndPsychomotorAssessmentRepository = ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();
            var affectiveAndPsychomotorAssessmentList = affectiveAndPsychomotorAssessmentRepository.Where(
                       s =>
                           s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                           s.Student.SchoolInfoId == schoolId).ToList();

            foreach (var item in affectiveAndPsychomotorAssessmentList)
            {
                affectiveAndPsychomotorAssessmentRepository.Delete(item);
            }

            var armRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var acceptedDateForStudentsTermedAsNew = DateTime.Now.AddDays(-40);
            var allStudents =
                studentRepository.Where(
                    s =>
                        (s.Status == Common.Shared.StudentStatus.Fresher ||
                         s.Status == Common.Shared.StudentStatus.Returning) && s.SchoolInfoId == schoolId 
                         && s.DateOfAdmsn < acceptedDateForStudentsTermedAsNew,
                    a => a.ClassArm.Classs).ToList();

            foreach (var eachStudent in allStudents)
            {
                var previousClass = Previous(eachStudent.CurrentClass);
                var currentStudentArm = eachStudent.ClassArm.ArmName;
                var studentCurrentArmId = eachStudent.ClassArmId;
                var previousClassArmModel =
                    armRepository.Find(
                        a =>
                            a.ArmName == currentStudentArm && a.Classs.ClassName == previousClass);
                eachStudent.CurrentClass = previousClass;
                var previousClassArmId = previousClassArmModel.Id;
                eachStudent.ClassArmId = previousClassArmId;
                studentRepository.Update(eachStudent);
            }

            return true;
        }



        public bool UndoRecentStudentsPromotion2nd()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var studentAcademicProgressHistoryRepository =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();

                var studentAcademicProgressHistoryList = studentAcademicProgressHistoryRepository.Where(
                        s =>
                            s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                            s.Classs.SchoolInfoId == schoolId).ToList();

            foreach (var item in studentAcademicProgressHistoryList)
            {
                studentAcademicProgressHistoryRepository.Delete(item);
            }

            var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
            var currentStudentAssessmentList =
                   studentAccessmentRepository.Where(
                       s =>
                           s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                           s.Student.SchoolInfoId == schoolId).ToList(); //.Sum(t => t.Ca1 + t.Ca2 + t.Ca3 + t.Exams);

            foreach (var item in currentStudentAssessmentList)
            {
                studentAccessmentRepository.Delete(item);
            }


            var affectiveAndPsychomotorAssessmentRepository = ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();
            var affectiveAndPsychomotorAssessmentList = affectiveAndPsychomotorAssessmentRepository.Where(
                       s =>
                           s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                           s.Student.SchoolInfoId == schoolId).ToList();

            foreach (var item in affectiveAndPsychomotorAssessmentList)
            {
                affectiveAndPsychomotorAssessmentRepository.Delete(item);
            }

            var armRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var allStudents =
                studentRepository.Where(
                    s =>
                        (s.Status == Common.Shared.StudentStatus.Fresher ||
                         s.Status == Common.Shared.StudentStatus.Returning) && s.SchoolInfoId == schoolId,
                    a => a.ClassArm.Classs).ToList();

            foreach (var eachStudent in allStudents)
            {
                var previousClass = Previous(eachStudent.CurrentClass);
                var currentStudentArm = eachStudent.ClassArm.ArmName;
                var studentCurrentArmId = eachStudent.ClassArmId;
                var previousClassArmModel =
                    armRepository.Find(
                        a =>
                            a.ArmName == currentStudentArm && a.Classs.ClassName == previousClass);
                eachStudent.CurrentClass = previousClass;
                var previousClassArmId = previousClassArmModel.Id;
                eachStudent.ClassArmId = previousClassArmId;
                studentRepository.Update(eachStudent);
            }

            return true;
        }


        public bool UndoRecentStudentsPromotion()
        {



            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var academicSessionRepository = ServiceLocator.Get<IGenericRepository<TxAcademicSession, short>>();

            var currentRegisteredAcademicSessions = academicSessionRepository.Where(d => d.SchoolInfoId == schoolId);
            var maxSession = 0;
            var maxSessionId = 0;
            foreach (var each in currentRegisteredAcademicSessions)
            {
                var sampleSession = Convert.ToInt16(each.Session.Substring(each.Session.Length - 4));
                if (sampleSession > maxSession)
                {
                    maxSession = sampleSession;
                    maxSessionId = each.Id;
                }
            }
            var lastSessionId = (short)maxSessionId;
            var currentSession = academicSessionRepository.Find(lastSessionId);
            currentSession.Status = Common.Shared.TimeStatus.Current;
            academicSessionRepository.Update(currentSession);

            var associatedTerms = yearlyAndTermlyAssessmentStatusRepository.Where(d => d.SessionId == lastSessionId);
            var supposedCurrentTerm = associatedTerms.OrderBy(g => g.TermStatus).Last();

            supposedCurrentTerm.TermlyAccessmentStatus = TermlyAccessmentStatus.Current;
            yearlyAndTermlyAssessmentStatusRepository.Update(supposedCurrentTerm);

            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var allStudents =
                studentRepository.Where(
                    s =>
                        (s.Status == Common.Shared.StudentStatus.Fresher ||
                         s.Status == Common.Shared.StudentStatus.Returning) && s.SchoolInfoId == schoolId,
                    a => a.ClassArm.Classs);
            var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
            foreach (var eachStudent in allStudents)
            {
                var currentStudentAssessment =
                    studentAccessmentRepository.Where(
                        s =>
                            s.YearlyAndTermlyStatus.SessionId == lastSessionId &&
                            s.StudentId == eachStudent.Id); //.Sum(t => t.Ca1 + t.Ca2 + t.Ca3 + t.Exams);

                var perTermAssessment =
                    currentStudentAssessment.GroupBy(d => d.YearlyAndTermlyStatus.TermStatus).ToList();
                var noOfTerm = perTermAssessment.Count();
                var yearlyTotalAverage = 0.0;

                foreach (var item in perTermAssessment)
                {
                    var termlyAverage = item.Sum(t => t.Ca1 + t.Ca2 + t.Ca3 + t.Exams) / item.Count();
                    yearlyTotalAverage = termlyAverage + yearlyTotalAverage;
                }
                var overalAverage = yearlyTotalAverage / noOfTerm;
                if (overalAverage >= 40)
                {
                    var armRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
                    var previousClass = Previous(eachStudent.CurrentClass);
                    var currentStudentArm = eachStudent.ClassArm.ArmName;
                    var studentCurrentArmId = eachStudent.ClassArmId;
                    var previousClassArmModel =
                        armRepository.Find(
                            a =>
                                a.ArmName == currentStudentArm && a.Classs.ClassName == previousClass &&
                                a.Classs.SchoolInfoId == schoolId);
                    eachStudent.CurrentClass = previousClass;
                    var previousClassArmId = previousClassArmModel.Id;
                    eachStudent.ClassArmId = previousClassArmId;
                    studentRepository.Update(eachStudent);

                    //var currentStudentAssessmentRecordList =
                    //                studentAccessmentRepository.Where(d => d.StudentId == eachStudent.Id).ToList();
                    //foreach (var eachAssessment in currentStudentAssessmentRecordList)
                    //{
                    //    eachAssessment.ClassArmId = previousClassArmId;
                    //    studentAccessmentRepository.Update(eachAssessment);
                    //}

                    //var affectiveAndPsychomotiveRepository =
                    //    ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();
                    //var currentStudentPsychomotorAndAffectiveRecordList =
                    //       affectiveAndPsychomotiveRepository.Where(d => d.StudentId == eachStudent.Id).ToList();
                    //foreach (var eachAssessment in currentStudentPsychomotorAndAffectiveRecordList)
                    //{
                    //    eachAssessment.ClassArmId = previousClassArmId;
                    //    affectiveAndPsychomotiveRepository.Update(eachAssessment);
                    //}

                }
            }
            return true;
        }

        private string CheckFormTeacherAssessmentCompletion()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var teacherClassArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
            var teacherClassArmAssignment =
                teacherClassArmAssignmentRepository.Where(
                    t =>
                        t.Employee.SchoolId == schoolId &&
                        t.ArmTermlyAssessmentStatus == ArmTermlyAssessmentStatus.Uncompleted,
                    t => t.Employee.User, t => t.Arm.Classs)?.ToList();

            if (teacherClassArmAssignment != null && teacherClassArmAssignment.Any())
            {
                return "Failed. One or more teacher has not completed their student assessment for this term.";
            }
            return "Success";
        }

        public string StudentsPromotionValidations()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
            var isStudentAssessmentEverPerformed =
                studentAccessmentRepository.Count(c => c.Student.SchoolInfoId == schoolId);
            if (!(isStudentAssessmentEverPerformed > 1))
            {
                return "Failed! You have not done any Students assessment yet";
                // "You first need to run the first environmental preparation";
            }
            var currentSessionAssessment =
                studentAccessmentRepository.Where(
                    s =>
                        s.YearlyAndTermlyStatus.Session.Status == Common.Shared.TimeStatus.Current &&
                        s.YearlyAndTermlyStatus.Session.SchoolInfoId == schoolId,
                    s => s.YearlyAndTermlyStatus).ToList();
            var numberOfTerm =
                currentSessionAssessment.GroupBy(
                    s => s.YearlyAndTermlyStatus.TermStatus).Count();

            var academicSessionRepository = ServiceLocator.Get<IGenericRepository<TxAcademicSession, short>>();
            var academicSessionList = academicSessionRepository.Where(s => s.SchoolInfoId == schoolId).Count();
            if (academicSessionList == 1)
            {
                var startTerm =
                    studentAccessmentRepository.Where(s => s.Employee.SchoolId == schoolId)
                        .OrderBy(a => a.YearlyAndTermlyStatus.TermStatus)
                        .First();
                if (startTerm.YearlyAndTermlyStatus.TermStatus == TermStatus.First)
                {
                    if (numberOfTerm == 1)
                        return "The school has only done a single term and therefore you cannot run promotion";
                    if (numberOfTerm == 2)
                        return
                            "The school has only done two terms and therefore you cannot run promotion; until when its three";
                    if (numberOfTerm == 3)
                    {
                        var status = CheckFormTeacherAssessmentCompletion();
                        if (status == "Success")
                        {
                            return "Success";
                        }
                        return status;
                    }
                }
                else if (startTerm.YearlyAndTermlyStatus.TermStatus == TermStatus.Second)
                {
                    if (numberOfTerm == 1)
                        return "The school has only done a single term and therefore you cannot run promotion";
                    if (numberOfTerm == 2)
                    {
                        var status = CheckFormTeacherAssessmentCompletion();
                        if (status == "Success")
                        {
                            return "Success";
                        }
                        return status;
                    }
                }
                else if (startTerm.YearlyAndTermlyStatus.TermStatus == TermStatus.Third)
                {
                    if (numberOfTerm == 1)
                    {
                        var status = CheckFormTeacherAssessmentCompletion();
                        if (status == "Success")
                        {
                            return "Success";
                        }
                        return status;
                    }
                }
            }
            if (numberOfTerm == 1)
                return "The school has only done a single term and therefore you cannot run promotion";
            if (numberOfTerm == 2)
                return "The school has only done two terms and therefore you cannot run promotion; until when its three";
            if (numberOfTerm == 3)
            {
                var status = CheckFormTeacherAssessmentCompletion();
                if (status == "Success")
                {
                    return "Success";
                }
                return status;
            }
            else
            {
                var sessionList = GetSessionsAndTerm().Sessions.OrderByDescending(x => x.Id).Take(2).ToList();
                var lastSession = new TxAcademicSession
                {
                    Id = sessionList[1].Id,
                    SchoolInfoId = sessionList[1].SchoolInfoId,
                    Status = Common.Shared.TimeStatus.Current,
                    Session = sessionList[1].Session
                };

                academicSessionRepository.Delete(sessionList[0].Id);
                academicSessionRepository.Update(lastSession);
                UnitOfWork.Commit();
                return "You need to try again.";
            }
        }

        public static T Next<T>(T src) where T : struct
        {
            if (!typeof(T).IsEnum)
                throw new ArgumentException(String.Format("Argumnent {0} is not an Enum", typeof(T).FullName));

            T[] arr = (T[])Enum.GetValues(src.GetType());
            int j = Array.IndexOf<T>(arr, src) + 1;
            return (arr.Length == j) ? arr[0] : arr[j];
        }

        public static T Previous<T>(T src) where T : struct
        {
            if (!typeof(T).IsEnum)
                throw new ArgumentException(String.Format("Argumnent {0} is not an Enum", typeof(T).FullName));

            T[] arr = (T[])Enum.GetValues(src.GetType());
            int j = Array.IndexOf<T>(arr, src) - 1;
            return (arr.Length == j) ? arr[0] : arr[j];
        }

        public string ConfirmStudentsPromotion()
        {

            var registrationOfficerValidationResult = "";
            var schoolId = "";
            var userRole = Thread.CurrentPrincipal.IsInRole("Registry");
            if (userRole)
            {
                schoolId = GetSchoolIdForSchoolRegistrationOfficer();
                registrationOfficerValidationResult = StudentsPromotionValidations();
                //return 'Success' when everything is good.
            }
            else
            {
                schoolId = CurrentTeacherModel().SchoolId;
                registrationOfficerValidationResult = "Success";
            }
            if (registrationOfficerValidationResult == "Success")
            {
                var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
                var allStudents =
                    studentRepository.Where(
                        s =>
                            (s.Status == Common.Shared.StudentStatus.Fresher ||
                             s.Status == Common.Shared.StudentStatus.Returning) && s.SchoolInfoId == schoolId,
                        a => a.ClassArm.Classs).ToList();
                var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
                var yearlyAndTermlyStatusRepository =
                    ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
                //Pass Mark Calculations
                var currentSessionId =
                    yearlyAndTermlyStatusRepository.Find(
                        n =>
                            n.Session.SchoolInfoId == schoolId &&
                            n.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).SessionId;
                var passMarkList =
                    yearlyAndTermlyStatusRepository.Where(
                        n => n.SessionId == currentSessionId).Select(z => z.MinPassMark).ToList();
                float totalPassMark = 0;
                float termCounter = 0;

                foreach (var item in passMarkList)
                {
                    var mark = item == 0 ? 0f : item ?? 40f;
                    totalPassMark = totalPassMark + mark;
                    termCounter = termCounter + 1;
                }
                var averagePassMark = totalPassMark / termCounter;
                //End
                foreach (var eachStudent in allStudents)
                {
                    var currentStudentAssessment =
                        studentAccessmentRepository.Where(
                            s =>
                                s.YearlyAndTermlyStatus.SessionId == currentSessionId &&
                                s.StudentId == eachStudent.Id).ToList(); //.Sum(t => t.Ca1 + t.Ca2 + t.Ca3 + t.Exams);

                    var studentSessionAverageScore =
                        currentStudentAssessment.Average(t => t.Ca1 + t.Ca2 + t.Ca3 + t.Exams);
                    if (studentSessionAverageScore < averagePassMark)
                    {
                        continue;
                    }

                    if (eachStudent.ClassArm.Classs.ClassName == CurrentClass.Ss3)
                    {
                        eachStudent.Status = Common.Shared.StudentStatus.Previous;
                        studentRepository.Update(eachStudent);
                    }
                    else
                    {
                        var armRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
                        var nextClass = Next(eachStudent.ClassArm.Classs.ClassName);
                        var currentStudentArm = eachStudent.ClassArm.ArmName;

                        var nextClassArm =
                            armRepository.Find(
                                a =>
                                    a.ArmName == currentStudentArm && a.Classs.ClassName == nextClass &&
                                    a.Classs.SchoolInfoId == schoolId);
                        var execusionPoint = 0;
                        if (execusionPoint == 0 && nextClassArm == null)
                        {
                            var highestClass =
                                ServiceLocator.Get<IGenericRepository<RxClasss, int>>()
                                    .Where(c => c.SchoolInfoId == schoolId)
                                    .OrderBy(v => v.ClassName)
                                    .Last()
                                    .ClassName; //Enum.GetValues(typeof(CurrentClass)).Cast<CurrentClass>().Last();

                            while (execusionPoint == 0 && nextClassArm == null)
                            //Handle the case where classes are not serial
                            {
                                if (nextClass >= highestClass)
                                {
                                    eachStudent.Status = Common.Shared.StudentStatus.Previous;
                                    studentRepository.Update(eachStudent);
                                    execusionPoint = 1;
                                }
                                else
                                {
                                    nextClassArm = armRepository.Find(a =>
                                        a.ArmName == currentStudentArm && a.Classs.ClassName == nextClass &&
                                        a.Classs.SchoolInfoId == schoolId);

                                    if (nextClassArm == null)
                                        nextClass = Next(nextClass);
                                }
                            }
                        }

                        if (execusionPoint == 1) //Signifies the final class
                        {
                            continue;
                        }
                        var nextClassArmId = nextClassArm.Id;
                        eachStudent.CurrentClass = nextClass;
                        if (nextClass == CurrentClass.Ss1)
                        {
                            eachStudent.ClassCategory = ClassCategoryStatus.Science;
                        }
                        eachStudent.ClassArmId = nextClassArmId;
                        studentRepository.Update(eachStudent);
                    }
                } //
                var academicSessionRepository = ServiceLocator.Get<IGenericRepository<TxAcademicSession, short>>();

                var currentSession =
                    academicSessionRepository.Find(
                        r => r.Status == Common.Shared.TimeStatus.Current && r.SchoolInfoId == schoolId);
                currentSession.Status = Common.Shared.TimeStatus.Previous;

                var currentTermStatus =
                    yearlyAndTermlyStatusRepository.Find(
                        y =>
                            y.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                            y.Session.SchoolInfoId == schoolId);
                currentTermStatus.TermlyAccessmentStatus = TermlyAccessmentStatus.Previous;

                academicSessionRepository.Update(currentSession);
                yearlyAndTermlyStatusRepository.Update(currentTermStatus);

                var sessionFirstComonent = int.Parse(currentSession.Session.Split('/')[0].Trim()) + 1;
                var sessionSecondComonent = int.Parse(currentSession.Session.Split('/')[1].Trim()) + 1;
                var newSession = sessionFirstComonent + "/" + sessionSecondComonent;
                var newSessionObj = new TxAcademicSession
                {
                    Session = newSession,
                    Status = Common.Shared.TimeStatus.Current,
                    SchoolInfoId = currentSession.SchoolInfoId
                };
                academicSessionRepository.Insert(newSessionObj);
                return "Succeeded";
            }
            return "Failed";
        }

        public bool DeleteAcademicSession(byte id)
        {
            var academicSessionRepository = ServiceLocator.Get<IGenericRepository<TxAcademicSession, short>>();
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var hasThisSessionHasExamsHavingProcessed =
                yearlyAndTermlyAssessmentStatusRepository.Find(p => p.SessionId == id);
            if (hasThisSessionHasExamsHavingProcessed != null)
            {
                return false;
            }
            academicSessionRepository.Delete(id);
            return true;
        }

        public IList<StudentFieldExtraction4AccountingDto> GetStudentByAdmissionNoForAccountant(string searchParameter)
        {
            var freshiersList = GetFreshiersList();
            var currentStudent = freshiersList.Where(x => x.AdmisionNo == searchParameter).ToList();
            return currentStudent;
            //var schoolId = CurrentTeacherModel().SchoolId;
            //var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            //var student = studentRepository.Where(s => s.IdNo == searchParameter && s.SchoolInfoId == schoolId,
            //    s => s.ClassArm.Classs, u => u.User).ToList();
            //return student.Select(n => new StudentFieldExtraction4AccountingDto
            //{
            //    ArmName = n.ClassArm.ArmName,
            //    StudentId = n.Id,
            //    StudentStatus = n.Status,
            //    ClassCategoryStatus = n.ClassCategory,
            //    ClassLabel = n.ClassArm.Classs.ClassLabel,
            //    FullName = n.User.FullName,
            //    AdmisionNo = n.IdNo
            //}).ToList();
        }

        public IEnumerable<StudentDetailsDto> GetStudentByAdmissionNo(string searchParameter)
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var student = studentRepository.Where(s => s.IdNo == searchParameter && s.SchoolInfoId == schoolId,
                s => s.ClassArm.Classs, u => u.User);
            return Mapper.Map<List<StudentDetailsDto>>(student);
        }

        public IEnumerable<StudentDetailsDto> GetArmStudentForAccountant(string concatParameters)
        {

            var eachValue = concatParameters.Split(',').ToList();
            var currentclass = (CurrentClass)int.Parse(eachValue[0]);
            var armName = (ArmName)int.Parse(eachValue[2]);
            var schoolId = CurrentTeacherModel().SchoolId;
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            //No need for school Id here.
            var armStudents =
                studentRepository.Where(
                    s =>
                        s.ClassArm.Classs.ClassName == currentclass && s.ClassArm.ArmName == armName &&
                        s.SchoolInfoId == schoolId, r => r.ClassArm.Classs, r => r.User);
            return Mapper.Map<List<StudentDetailsDto>>(armStudents);
        }

        public IEnumerable<StudentDetailsDto> GetArmStudents(string concatParameters)
        {

            var eachValue = concatParameters.Split(',').ToList();
            var currentclass = (CurrentClass)int.Parse(eachValue[0]);
            var armName = (ArmName)int.Parse(eachValue[2]);
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            //No need for school Id here.
            var armStudents =
                studentRepository.Where(
                    s =>
                        s.ClassArm.Classs.ClassName == currentclass && s.ClassArm.ArmName == armName &&
                        s.SchoolInfoId == schoolId, r => r.ClassArm.Classs, r => r.User);
            return Mapper.Map<List<StudentDetailsDto>>(armStudents);
        }


        public bool DisableSchoolUsersBySchoolRegistrarId(string userId)
        {
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var userModel = userRepository.Find(i => i.Id == userId);
            userModel.AccessStatus = AccessStatus.Disable;
            userRepository.Update(userModel);
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            //No need for school Id here.

            var schoolId = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>()
                .Find(x=> x.SchoolRegistrationOfficer.UserId == userId).SchoolRegistrationOfficerId;

            var armStudents =
                studentRepository.Where(
                    s =>
                        s.SchoolInfoId == schoolId, r => r.User).ToList();
            foreach (var item in armStudents)
            {
                var model = item.User;
                model.AccessStatus = AccessStatus.Disable;
                userRepository.Update(model);
            }
            var teachers = teacherRepository.Where(x => x.SchoolId == schoolId, r => r.User).ToList();

            foreach (var item in teachers)
            {
                var model = item.User;
                model.AccessStatus = AccessStatus.Disable;
                userRepository.Update(model);
            }
            return true;
        }
        public bool EnableSchoolUsersBySchoolRegistrarId(string userId)
        {
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var userModel = userRepository.Find(i => i.Id == userId);
            userModel.AccessStatus = AccessStatus.Active;
            userRepository.Update(userModel);


            var schoolId = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>()
                .Find(x => x.SchoolRegistrationOfficer.UserId == userId).SchoolRegistrationOfficerId;
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            //No need for school Id here.
            var armStudents =
                studentRepository.Where(
                    s =>
                        s.SchoolInfoId == schoolId, r => r.User).ToList();
            foreach (var item in armStudents)
            {
                var model = item.User;
                model.AccessStatus = AccessStatus.Active;
                userRepository.Update(model);
            }
            var teachers = teacherRepository.Where(x => x.SchoolId == schoolId, r => r.User).ToList();

            foreach (var item in teachers)
            {
                var model = item.User;
                model.AccessStatus = AccessStatus.Active;
                userRepository.Update(model);
            }
            return true;
        }


        public bool DisableUserById(int id)
        {
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var userModel = teacherRepository.Find(i => i.Id == id, s => s.User).User;
            userModel.AccessStatus = AccessStatus.Disable;
            userRepository.Update(userModel);
            return true;
        }

        public bool UpdateSchoolLabel(SchoolLevelCategoriesLabelDto model)
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var schoolLevelCategoriesLabelRepository =
                ServiceLocator.Get<IGenericRepository<SubjectRxCategories, short>>();
            var schoolLabel = schoolLevelCategoriesLabelRepository.Where(t => t.SchoolId == schoolId)?.ToList();
            if (schoolLabel != null && schoolLabel.Any())
            {
                foreach (var item in schoolLabel)
                {
                    if (item.SubjectStatus == SubjectStatus.General)
                    {
                        item.SubjectStatusLabel = model.GeneralSujectLabel;
                    }
                    else if (item.SubjectStatus == SubjectStatus.PlayClass)
                    {
                        item.SubjectStatusLabel = model.PlayClassLabel;
                    }
                    else if (item.SubjectStatus == SubjectStatus.PreNursery)
                    {
                        item.SubjectStatusLabel = model.PreNurseryLabel;
                    }
                    else if (item.SubjectStatus == SubjectStatus.Nursery)
                    {
                        item.SubjectStatusLabel = model.NurseryLabel;
                    }
                    else if (item.SubjectStatus == SubjectStatus.Primary)
                    {
                        item.SubjectStatusLabel = model.PrimaryLabel;
                    }
                    else if (item.SubjectStatus == SubjectStatus.Jss)
                    {
                        item.SubjectStatusLabel = model.JssLabel;
                    }
                    else if (item.SubjectStatus == SubjectStatus.Elective)
                    {
                        item.SubjectStatusLabel = model.ElectiveSubjectLabel;
                    }
                    schoolLevelCategoriesLabelRepository.Update(item);
                }
            }
            return true;
        }

        private string GetSchoolIdForProprietor()
        {
            var schoolRegistrationOfficerRepository =
                ServiceLocator.Get<IGenericRepository<SchoolRegistrationOfficer>>();
            return schoolRegistrationOfficerRepository.GetAll().Max(j => j.Id);
        }

        private string GetSchoolIdForSchoolRegistrationOfficer()
        {
            var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            var schoolRegistrationOfficerRepository =
                ServiceLocator.Get<IGenericRepository<SchoolRegistrationOfficer>>();
            return schoolRegistrationOfficerRepository.Find(u => u.UserId == userId).Id;
        }

        public IList<SubjectCategoriesDto> GetSchoolLabel()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var schoolLevelCategoriesLabelRepository =
                ServiceLocator.Get<IGenericRepository<SubjectRxCategories, short>>();
            var schoolLabel = schoolLevelCategoriesLabelRepository.Where(t => t.SchoolId == schoolId);
            return Mapper.Map<List<SubjectCategoriesDto>>(schoolLabel);

        }

        public bool EnableUserById(int id)
        {
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var userModel = teacherRepository.Find(i => i.Id == id, t => t.User).User;
            userModel.AccessStatus = AccessStatus.Active;
            userRepository.Update(userModel);
            return true;
        }

        public IEnumerable<SelectListItem> ClassDropDown()
        {
            var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var userRole = Thread.CurrentPrincipal.IsInRole("Registry");
            var schoolId = "";
            if (userRole)
            {
                schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            }
            else
            {
                schoolId = CurrentTeacherModel().SchoolId;
            }

            var classes =
                classRepository.Where(e => e.SchoolInfoId == schoolId)?
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.ClassLabel });
            return classes;
        }

        public ClassAndArmDropDownDto AccountingClassAndArmDropDown()
        {
            var armRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var schoolId = CurrentTeacherModel().SchoolId;
            var arms =
                armRepository.Where(e => e.Classs.SchoolInfoId == schoolId)?
                    .OrderBy(f => f.ArmName)
                    .GroupBy(f => f.ArmName)
                    .Select(f => f.First())
                    .Select(
                        r =>
                            new SelectListItem
                            {
                                Value = ((byte)(ArmName)Enum.Parse(typeof(ArmName), r.ArmName.ToString())).ToString(),
                                Text = r.ArmName.ToString()
                            });

            var classAndArmDropDown = ServiceLocator.Get<ClassAndArmDropDownDto>();
            classAndArmDropDown.ArmName = arms;
            var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var classes =
                classRepository.Where(e => e.SchoolInfoId == schoolId)?
                    .Select(
                        c =>
                            new SelectListItem
                            {
                                Value =
                                    ((byte)(CurrentClass)Enum.Parse(typeof(CurrentClass), c.ClassName.ToString()))
                                        .ToString() + "," + c.ClassLabel,
                                Text = c.ClassLabel
                            });
            classAndArmDropDown.ClassName = classes;
            return classAndArmDropDown;
        }

        public ClassAndArmDropDownDto StudentIndexClassAndArmDropDown()
        {
            var armRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var userRole = Thread.CurrentPrincipal.IsInRole("Registry");
            var schoolId = "";
            if (userRole)
            {
                schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            }
            else
            {
                schoolId = CurrentTeacherModel().SchoolId;
            }
            var arms =
                armRepository.Where(e => e.Classs.SchoolInfoId == schoolId)?
                    .OrderBy(f => f.ArmName)
                    .GroupBy(f => f.ArmName)
                    .Select(f => f.First())
                    .Select(
                        r =>
                            new SelectListItem
                            {
                                Value = ((byte)(ArmName)Enum.Parse(typeof(ArmName), r.ArmName.ToString())).ToString(),
                                Text = r.ArmName.ToString()
                            });

            var classAndArmDropDown = ServiceLocator.Get<ClassAndArmDropDownDto>();
            classAndArmDropDown.ArmName = arms;
            var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var classes =
                classRepository.Where(e => e.SchoolInfoId == schoolId)?.OrderBy(b => b.ClassName)
                    .Select(
                        c =>
                            new SelectListItem
                            {
                                Value =
                                    ((byte)(CurrentClass)Enum.Parse(typeof(CurrentClass), c.ClassName.ToString()))
                                        .ToString() + "," + c.ClassLabel,
                                Text = c.ClassLabel
                            });
            classAndArmDropDown.ClassName = classes;
            return classAndArmDropDown;
        }

        public bool CreateSchool(string schoolName, string token, string key)
        {
            var schoolSubjectCategoryRepository = ServiceLocator.Get<IGenericRepository<SubjectRxCategories, short>>();
            var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var schoolModel = ServiceLocator.Get<SchoolTyInfo>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var lastRecord = schoolInfoRepository.GetAll().ToList();
            var lastSerialNo = 0;
            if (lastRecord.Any())
            {
                lastSerialNo = lastRecord.Max(s => s.SsNo);
            }
            lastSerialNo = (byte)(lastSerialNo + 1);
            schoolModel.SsNo = (byte)lastSerialNo;
            var newSNo = (short)(schoolSubjectCategoryRepository.Count() + 1);
            schoolModel.SchoolRegistrationOfficerId = schoolId;
            schoolModel.SchoolName = schoolName;
            schoolModel.SmsApiToken = FreePlugIn.Encrypt(token ?? Guid.NewGuid().ToString(), true);
            schoolModel.SmsApiKey = FreePlugIn.Encrypt(key ?? Guid.NewGuid().ToString(), true);
            schoolInfoRepository.Update(schoolModel);
            UnitOfWork.SaveChanges();

            var smsPaymentBalanceRepo = ServiceLocator.Get<IGenericRepository<SmsPaymentBalance, int>>();
            var smsPurchaseRepo = ServiceLocator.Get<IGenericRepository<SmsPurchase, int>>();

            var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            var agentId =
                ServiceLocator.Get<IGenericRepository<Agent, short>>().Find(c => c.UserId == userId)?.Id;

            if (agentId == null)
            {
                agentId =
                    ServiceLocator.Get<IGenericRepository<Agent, short>>()
                        .Find(x => x.User.UserName.Trim().ToLower() == "agentofficer")
                        .Id;
            }
            var smsPurcharseModel = new SmsPurchase
            {
                SchoolSmsId = schoolId,
                AmountPurchase = 10,
                AgentUserId = (short)agentId,
                BalB4Purchase = 0,
                PaymentDate = DateTime.Now,
                TimeStatus = Common.Shared.TimeStatus.Current
            };
            smsPurchaseRepo.Insert(smsPurcharseModel);

            var paymentBalanceModel = new SmsPaymentBalance
            {
                Amount = 10,
                SchoolSmsId = schoolId,
                PricePerSms = 2.7f
            };
            smsPaymentBalanceRepo.Insert(paymentBalanceModel);

            var subjectCategoryList = new List<SubjectRxCategories>
            {
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.PreNursery,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Nursery,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.General,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Primary,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Jss,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.AllSeniors,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Science,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Art,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Commercial,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Elective,
                    SchoolId = schoolId,
                    Id = newSNo,
                }
            };
            foreach (var each in subjectCategoryList)
            {
                var getSubjectCategories = schoolSubjectCategoryRepository.GetAll();
                var sNo = schoolSubjectCategoryRepository.Count();
                var counter = 0;
                var subjectCategories = getSubjectCategories as IList<SubjectRxCategories> ??
                                        getSubjectCategories.ToList();
                if (subjectCategories.Any())
                {
                    var lastRecordId = subjectCategories.OrderBy(r => r.Id).Last().Id;
                    if (lastRecordId != sNo)
                    {
                        foreach (var eachSession in subjectCategories)
                        {
                            counter = counter + 1;
                            if (counter != eachSession.Id)
                            {
                                each.Id = (short)counter;
                                schoolSubjectCategoryRepository.Insert(each);
                                UnitOfWork.SaveChanges();
                                break;
                            }
                        }
                    }
                    else
                    {
                        sNo = sNo + 1;
                        each.Id = (short)sNo;
                        schoolSubjectCategoryRepository.Insert(each);
                        UnitOfWork.SaveChanges();
                    }
                }
                else
                {
                    sNo = sNo + 1;
                    each.Id = (short)sNo;
                    schoolSubjectCategoryRepository.Insert(each);
                    UnitOfWork.SaveChanges();
                }

            }
            return true;
        }

        public bool CreateTotalAttendance(int noOfDaysInThisTerm)
        {
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyAssessmentStatusModel =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    f => f.Session.SchoolInfoId == schoolId &&
                         (f.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                          f.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing));
            if (yearlyAndTermlyAssessmentStatusModel != null)
            {
                yearlyAndTermlyAssessmentStatusModel.TotalAttendance = (byte)noOfDaysInThisTerm;
                yearlyAndTermlyAssessmentStatusRepository.Update(yearlyAndTermlyAssessmentStatusModel);
                return true;
            }
            return false;
        }

        public int? GetCurrentTotalAttendance()
        {
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyAssessmentStatusModel =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    f => f.Session.SchoolInfoId == schoolId && (
                        f.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                        f.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing));

            return yearlyAndTermlyAssessmentStatusModel?.TotalAttendance;
        }

        public IEnumerable<SelectListItem> ReligionStatus(Religion religion)
        {
            var startClas = new List<SelectListItem>
            {
                new SelectListItem {Text = religion.ToString(), Value = ((int) religion).ToString()}
            };
            var enumToList = EnumsAndExtensionMethods.GetEnumToList<Religion>();
            startClas.AddRange(from item in enumToList
                               where item != religion
                               select new SelectListItem { Text = item.ToString(), Value = ((int)item).ToString() });
            return startClas;
        }

        //public byte HasBothTwoReligionsAlreadyBeenSaved()
        //{
        //    var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
        //    var schoolId = GetSchoolId();
        //    return (byte)subjectRepository.Count(
        //        s =>
        //            s.SchoolId == schoolId &&
        //            (s.SubjectCategory.SubjectStatus == SubjectStatus.Islam || s.SubjectCategory.SubjectStatus == SubjectStatus.Christianity));
        //}
        //public bool DoesIslamicReligionSubjectAreadyExist()
        //{
        //    var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
        //    var schoolId = GetSchoolId();
        //    var check = subjectRepository.Find(
        //        s => s.SubjectCategory.SchoolId == schoolId && s.SubjectCategory.SubjectStatus == SubjectStatus.Islam);
        //    return check == null;
        //}

        //public bool DoesChristianityReligionSubjectAreadyExist()
        //{
        //    var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
        //    var schoolId = GetSchoolId();
        //    var check = subjectRepository.Find(
        //        s => s.SubjectCategory.SchoolId == schoolId && s.SubjectCategory.SubjectStatus == SubjectStatus.Christianity);
        //    return check == null;
        //}

        //public bool IslamicSubjectUpdateValidation(string subjectName, int id)
        //{
        //    var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
        //    var schoolId = GetSchoolId();
        //    var doesSubjectExistAgainstAnotherRecord =
        //        subjectRepository.Find(
        //            sr =>
        //                sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() && sr.SubjectCategory.SchoolId == schoolId &&
        //                (sr.SubjectCategory.SubjectStatus == SubjectStatus.Trade || sr.SubjectCategory.SubjectStatus != SubjectStatus.Others ||
        //                sr.SubjectCategory.SubjectStatus != SubjectStatus.General || sr.SubjectCategory.SubjectStatus != SubjectStatus.AllSeniors || sr.SubjectCategory.SubjectStatus != SubjectStatus.Art ||
        //                 sr.SubjectCategory.SubjectStatus != SubjectStatus.Science || sr.SubjectCategory.SubjectStatus == SubjectStatus.Islam || sr.SubjectCategory.SubjectStatus == SubjectStatus.Christianity));
        //    if (doesSubjectExistAgainstAnotherRecord != null)
        //    {
        //        if (doesSubjectExistAgainstAnotherRecord.Id == id)
        //        {
        //            return true;
        //        }
        //        else
        //        {
        //            return false;
        //        }
        //    }
        //    return true;
        //}

        //public bool ChristianitySubjectUpdateValidation(string subjectName, int id)
        //{
        //    var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
        //    var schoolId = GetSchoolId();
        //    var doesSubjectExistAgainstAnotherRecord =
        //        subjectRepository.Find(
        //            sr =>
        //                sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() && sr.SubjectCategory.SchoolId == schoolId &&
        //                (sr.SubjectCategory.SubjectStatus == SubjectStatus.Trade || sr.SubjectCategory.SubjectStatus != SubjectStatus.Others ||
        //                sr.SubjectCategory.SubjectStatus != SubjectStatus.General || sr.SubjectCategory.SubjectStatus != SubjectStatus.AllSeniors || sr.SubjectCategory.SubjectStatus != SubjectStatus.Art ||
        //                 sr.SubjectCategory.SubjectStatus != SubjectStatus.Science || sr.SubjectCategory.SubjectStatus == SubjectStatus.Islam || sr.SubjectCategory.SubjectStatus == SubjectStatus.Christianity));
        //    if (doesSubjectExistAgainstAnotherRecord != null)
        //    {
        //        if (doesSubjectExistAgainstAnotherRecord.Id == id)
        //        {
        //            return true;
        //        }
        //        else
        //        {
        //            return false;
        //        }
        //    }
        //    return true;
        //}
        public IEnumerable<SelectListItem> CurrentsClassForEditingSubject(CurrentClass className)
        {
            var currentClass = new List<SelectListItem>
            {
                new SelectListItem {Text = className.ToString(), Value = ((int) className).ToString()}
            };
            var enumToList = EnumsAndExtensionMethods.GetEnumToList<CurrentClass>();
            currentClass.AddRange(from item in enumToList
                                  where item != className
                                  select new SelectListItem { Text = item.ToString(), Value = ((int)item).ToString() });
            return currentClass;
        }

        //private IEnumerable<ClassLevelSubjectDto> ClassLevelSubjects(List<RxSubject> subjects, string schoolId)
        //{
        //    var schoolLevelCategoriesLabelRepository = ServiceLocator.Get<IGenericRepository<SubjectRxCategories, int>>();
        //    var schoolLabel = schoolLevelCategoriesLabelRepository.Where(t => t.SchoolId == schoolId, f=>f.SubjectStatusLabel);
        //    var classLevelSubjectLabel = new List<ClassLevelSubjectDto>();
        //    foreach (var item in subjects)
        //    {
        //        var statusLabel = "";
        //        if (item.SubjectCategory.SubjectStatus == SubjectStatus.PreNursery)
        //        {
        //            statusLabel = schoolLabel.PreNurseryLabel;
        //        }
        //        else if (item.SubjectCategory.SubjectStatus == SubjectStatus.Nursery)
        //        {
        //            statusLabel = schoolLabel.NurseryLabel;
        //        }
        //        else if (item.SubjectCategory.SubjectStatus == SubjectStatus.Primary)
        //        {
        //            statusLabel = schoolLabel.PrimaryLabel;
        //        }
        //        else if (item.SubjectCategory.SubjectStatus == SubjectStatus.Jss)
        //        {
        //            statusLabel = schoolLabel.JssLabel;
        //        }
        //        else if (item.SubjectCategory.SubjectStatus == SubjectStatus.AllSeniors || item.SubjectCategory.SubjectStatus == SubjectStatus.Science
        //            || item.SubjectCategory.SubjectStatus == SubjectStatus.Art || item.SubjectCategory.SubjectStatus == SubjectStatus.Elective
        //            || item.SubjectCategory.SubjectStatus == SubjectStatus.Elective)
        //        {
        //            statusLabel = schoolLabel.SsLabel;
        //        }
        //        else if (item.SubjectCategory.SubjectStatus == SubjectStatus.General)
        //        {
        //            statusLabel = schoolLabel.GeneralSujectLabel;
        //        }
        //        else if (item.SubjectCategory.SubjectStatus == SubjectStatus.Commercial)
        //        {
        //            statusLabel = schoolLabel.ReligionSubjectLabel;
        //        }
        //        else if (item.SubjectCategory.SubjectStatus == SubjectStatus.Elective)
        //        {
        //            statusLabel = schoolLabel.CustomLevelLabel;
        //        }
        //        classLevelSubjectLabel.Add(new ClassLevelSubjectDto
        //        {
        //            Id = item.Id,
        //            LevelsName = statusLabel,
        //            SubjectName = item.SubjectName,
        //            TimeStatus = item.TimeStatus
        //        });
        //    }
        //    return classLevelSubjectLabel;
        //}
        public IEnumerable<SubjectDto> GetLevelsThatOfferThisSubject(string subjectName)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            List<RxSubject> subjects;
            if (subjectName == "AllSubjects")
            {
                subjects =
                    subjectRepository.Where(r => r.SubjectCategory.SchoolId == schoolId, c => c.SubjectCategory)
                        .ToList();
            }
            else
            {
                subjects = subjectRepository.Where(
                    r =>
                        r.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        r.SubjectCategory.SchoolId == schoolId, c => c.SubjectCategory).ToList();
            }
            return Mapper.Map<List<SubjectDto>>(subjects);
        }

        public IEnumerable<SubjectDto> SearchLevelsThatOfferThisSubject(string searchParameter)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();

            var userRole = Thread.CurrentPrincipal.IsInRole("Registry");
            var schoolId = "";
            if (userRole)
            {
                schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            }
            else
            {
                //Parent/Student direction
                var userId = Thread.CurrentPrincipal.Identity.GetUserId();
                var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
                schoolId = studentRepository.Find(f => f.UserId == userId).SchoolInfoId;
            }
            var subjects =
                subjectRepository.Where(
                    r =>
                        r.SubjectName.Trim().ToLower().Contains(searchParameter.Trim().ToLower()) &&
                        r.SubjectCategory.SchoolId == schoolId, h => h.SubjectCategory)?.ToList();
            return Mapper.Map<List<SubjectDto>>(subjects);
        }

        public string GetLastStudentAdminssionNumber()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var lastAdmissionNo =
                studentRepository.Where(s => s.SchoolInfoId == schoolId, d => d.User)?
                    .ToList()
                    .OrderByDescending(d => d.User.CreatedDate);
            if (lastAdmissionNo != null && lastAdmissionNo.Any()) return lastAdmissionNo.First()?.IdNo;
            return null;
        }

        public bool CommercialSubjectUpdateValidation(string subjectName, int id)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExistAgainstAnotherRecord =
                subjectRepository.Where(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId)?
                    .Select(r => r.SubjectCategory.SubjectStatus).ToList();

            if (doesSubjectExistAgainstAnotherRecord != null && doesSubjectExistAgainstAnotherRecord.Any())
            {
                if (doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.Science) ||
                    doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.AllSeniors) ||
                    doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.General)
                    || doesSubjectExistAgainstAnotherRecord.Contains(SubjectStatus.Art))
                {
                    return false;
                }
                return true;
            }
            return true;
        }

        public UserDto GetProprietorInfo()
        {

            var proprietorRepository = ServiceLocator.Get<IGenericRepository<Agent, int>>();
            var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            var model = proprietorRepository.Find(h => h.UserId == userId, j => j.User);
            var returnModel = Mapper.Map<UserDto>(model.User);
            returnModel.Addresses = model.FullAddress;
            returnModel.SchoolRegId = model.SchoolRegId;
            return returnModel;
        }

        public SchoolListForSystemAdminDto GetSchoolListForSuperAdmin()
        {
            var schoolRegistrationRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var noOfSchools = schoolRegistrationRepository.Count();
            var schools = schoolRegistrationRepository.GetAll(s => s.SchoolRegistrationOfficer.AgentUser,
                z => z.SchoolRegistrationOfficer.User).Take(7);
            return new SchoolListForSystemAdminDto
            {
                SchoolInfo = Mapper.Map<List<SchoolInfoDto>>(schools),
                NoOfSchools = noOfSchools
            };
        }

        public IEnumerable<SchoolInfoDto> GetSchoolListForAgent()
        {
            var schoolRegistrationRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            var schools = schoolRegistrationRepository.Where(
                x => x.SchoolRegistrationOfficer.AgentUser.UserId == userId, s => s.SchoolRegistrationOfficer.AgentUser,
                z => z.SchoolRegistrationOfficer.User);
            return Mapper.Map<List<SchoolInfoDto>>(schools);
        }

        public bool UpdateProprietorInfo(UserDto model)
        {
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var user = userRepository.Find(u => u.Id == model.Id);
            user.FirstName = model.FirstName;
            user.MiddleName = model.MiddleName;
            user.SurName = model.SurName;
            user.Email = model.Email;
            user.PhoneNumber = model.PhoneNumber;
            userRepository.Update(user);
            var proprietorRepository = ServiceLocator.Get<IGenericRepository<Agent, int>>();
            var proprietor = proprietorRepository.Find(s => s.UserId == model.Id);
            proprietor.FullAddress = model.Addresses;
            proprietor.SchoolRegId = model.SchoolRegId;
            proprietorRepository.Update(proprietor);
            return true;
        }

        public bool DoesThisElectiveSubjectAlreadyExist(string subjectName)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExist =
                subjectRepository.Where(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        (sr.SubjectCategory.SubjectStatus == SubjectStatus.Elective)).FirstOrDefault();
            if (doesSubjectExist != null)
            {
                return false;
            }
            return true;
        }

        public IList<SelectListItem> GetTeachersForDropdown()
        {
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var teachers =
                teacherRepository.Where(
                    t =>
                        t.TimeStatus == Common.Shared.TimeStatus.Current && t.SchoolId == schoolId &&
                        t.EmployeeStatus == Common.Shared.EmployeeStatus.Teacher, c => c.User)?
                    .Select(r => new SelectListItem { Value = r.UserId, Text = r.User.FullName }).ToList();
            if (teachers != null && teachers.Any())
            {

            }
            return teachers;
        }

        public IEnumerable<TeacherSubjectArmAssignmentDto> GetTeacherAssignedSubjectsByTeacherId(
            string selectedTeacherId)
        {
            var teacherSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var teacherSubjectArmAssignmentList =
                teacherSubjectArmAssignmentRepository.Where(
                    u =>
                        u.Employee.SchoolId == schoolId &&
                        u.Employee.UserId == selectedTeacherId,
                    u => u.Employee.User, r => r.Subject, a => a.Arm.Classs);
            var result = Mapper.Map<List<TeacherSubjectArmAssignmentDto>>(teacherSubjectArmAssignmentList);
            return result;
        }

        public IEnumerable<TeacherClassArmAssignmentDto> GetTeacherAssignedArmsByTeacherId(string selectedTeacherId)
        {
            var teacherSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var teacherSubjectArmAssignmentList =
                teacherSubjectArmAssignmentRepository.Where(
                    u =>
                        u.Employee.SchoolId == schoolId &&
                        u.Employee.UserId == selectedTeacherId,
                    u => u.Employee.User, r => r.Arm.Classs);
            return Mapper.Map<List<TeacherClassArmAssignmentDto>>(teacherSubjectArmAssignmentList);
        }

        public bool TransferSubjectTeacherSubjectAssignment(List<SubjectIdsBto> subjectIds)
        {
            var teacherSubjectAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            var proposedTeacherId = subjectIds.Last().Id;
            var lastId = subjectIds.Last().Id;
            var lastIndex = subjectIds.Find(s => s.Id == lastId);
            subjectIds.Remove(lastIndex);
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            //var subjectId = 0;
            foreach (var item in subjectIds)
            {
                var assignmentId = int.Parse(item.Id);
                var teacherSubjectAssignmentModel = teacherSubjectAssignmentRepository.Find(assignmentId);
                var newTeacherId =
                    teacherRepository.Find(t => t.UserId == proposedTeacherId).Id;
                teacherSubjectAssignmentModel.EmployeeId = newTeacherId;
                teacherSubjectAssignmentRepository.Update(teacherSubjectAssignmentModel);


                var subjectId = teacherSubjectAssignmentModel.SubjectId;
                var armId = teacherSubjectAssignmentModel.ArmId;

                var asseessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();

                var assignedStudents =
                    asseessmentRepository.Where(
                        x =>
                            x.ClassArmId == armId && x.SubjectId == subjectId &&
                            x.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).ToList();

                foreach (var each in assignedStudents)
                {
                    each.EmployeeId = newTeacherId;
                    asseessmentRepository.Update(each);
                }
            }
            return true;
        }

        public bool TransferArmTeacherArmAssignment(List<SubjectIdsBto> subjectIds)
        {
            var teacherClassArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
            var proposedTeacherId = subjectIds.Last().Id;
            var lastId = subjectIds.Last().Id;
            var lastIndex = subjectIds.Find(s => s.Id == lastId);
            subjectIds.Remove(lastIndex);
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var currentTeacherId = 0;
            var proposedTeacherIdInt = 0;
            foreach (var item in subjectIds)
            {
                var assignmentId = int.Parse(item.Id);
                var teacherSubjectAssignmentModel = teacherClassArmAssignmentRepository.Find(assignmentId);
                var newTeacherId =
                    teacherRepository.Find(t => t.UserId == proposedTeacherId).Id;
                currentTeacherId = teacherSubjectAssignmentModel.EmployeeId;
                proposedTeacherIdInt = newTeacherId;
                teacherSubjectAssignmentModel.EmployeeId = newTeacherId;
                teacherClassArmAssignmentRepository.Update(teacherSubjectAssignmentModel);
            }
            var affectiveAndPsychomotorAssessmentRepository =
                ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();
            var listOfPsychoStudEntries =
                affectiveAndPsychomotorAssessmentRepository.Where(
                    c =>
                        c.FormMasterId == currentTeacherId &&
                        c.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).ToList();
            
            foreach (var each in listOfPsychoStudEntries)
            {
                each.FormMasterId = proposedTeacherIdInt;
                affectiveAndPsychomotorAssessmentRepository.Update(each);
            }
            return true;
        }

        public IList<SelectListItem> GetClassSubjectsForDropDown(string concatParameters)
        {
            //var parameterList = concatParameters.Split(',').ToList();
            var classId = int.Parse(concatParameters);
            var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var classModel = classRepository.Find(classId);
            var className = classModel.ClassName;
            var teacherSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            var getSubject = new List<SubjectDto>();

            var assignedSubjects =
                teacherSubjectArmAssignmentRepository.Where(
                    s => s.Arm.ClassId == classId && s.Subject.TimeStatus == Common.Shared.TimeStatus.Current,
                    a => a.Subject,
                    c => c.Arm.Classs)?.ToList();
            if (className == CurrentClass.PlayClass)
            {
                getSubject.AddRange(BasicClassesSubjectListCurrent(CurrentClass.PlayClass));
            }
            else if (className == CurrentClass.PreNursery)
            {
                getSubject.AddRange(BasicClassesSubjectListCurrent(CurrentClass.PreNursery));
            }
            else if (className == CurrentClass.Nursery1 || className == CurrentClass.Nursery2)
            {
                getSubject.AddRange(BasicClassesSubjectListCurrent(CurrentClass.Nursery1));
                getSubject.AddRange(ElectiveSubjectsCollections(classId, GetSchoolIdForSchoolRegistrationOfficer()));
            }
            else if (className == CurrentClass.Primary1 || className == CurrentClass.Primary2
                     || className == CurrentClass.Primary3 || className == CurrentClass.Primary4
                     || className == CurrentClass.Primary5 || className == CurrentClass.Primary6)
            {
                getSubject.AddRange(BasicClassesSubjectListCurrent(CurrentClass.Primary1));
                getSubject.AddRange(ElectiveSubjectsCollections(classId, GetSchoolIdForSchoolRegistrationOfficer()));
            }
            else if (className == CurrentClass.Jss1 || className == CurrentClass.Jss2
                     || className == CurrentClass.Jss3)
            {
                getSubject.AddRange(BasicClassesSubjectListCurrent(CurrentClass.Jss1));
                getSubject.AddRange(ElectiveSubjectsCollections(classId, GetSchoolIdForSchoolRegistrationOfficer()));
            }
            else if (className == CurrentClass.Ss1 || className == CurrentClass.Ss2
                     || className == CurrentClass.Ss3)
            {
                var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
                var schoolId = GetSchoolIdForSchoolRegistrationOfficer();

                var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
                if (
                    studentRepository.Where(
                        s => s.SchoolInfoId == schoolId && s.ClassCategory == ClassCategoryStatus.Science).Any())
                {
                    var scienceSubjectList =
                        subjectRepository.Where(
                            z =>
                                z.SubjectCategory.SchoolId == schoolId &&
                                z.SubjectCategory.SubjectStatus == SubjectStatus.Science &&
                                z.TimeStatus == Common.Shared.TimeStatus.Current).ToList();
                    getSubject.AddRange(Mapper.Map<List<SubjectDto>>(scienceSubjectList));
                }
                if (
                    studentRepository.Where(
                        s => s.SchoolInfoId == schoolId && s.ClassCategory == ClassCategoryStatus.Art).Any())
                {
                    var artSubjectList =
                        subjectRepository.Where(
                            z =>
                                z.SubjectCategory.SchoolId == schoolId &&
                                z.SubjectCategory.SubjectStatus == SubjectStatus.Art &&
                                z.TimeStatus == Common.Shared.TimeStatus.Current).ToList();
                    getSubject.AddRange(Mapper.Map<List<SubjectDto>>(artSubjectList));
                }
                if (
                    studentRepository.Where(
                        s => s.SchoolInfoId == schoolId && s.ClassCategory == ClassCategoryStatus.Commercial).Any())
                {
                    var commercialSubjectList =
                        subjectRepository.Where(
                            z =>
                                z.SubjectCategory.SchoolId == schoolId &&
                                z.SubjectCategory.SubjectStatus == SubjectStatus.Commercial &&
                                z.TimeStatus == Common.Shared.TimeStatus.Current).ToList();
                    getSubject.AddRange(Mapper.Map<List<SubjectDto>>(commercialSubjectList));
                }
                var subjectlist =
                    Mapper.Map<List<SubjectDto>>(subjectRepository.Where(s => s.SubjectCategory.SchoolId == schoolId
                                                                              &&
                                                                              s.TimeStatus ==
                                                                              Common.Shared.TimeStatus.Current && (
                                                                                  s.SubjectCategory.SubjectStatus ==
                                                                                  SubjectStatus.General ||
                                                                                  s.SubjectCategory.SubjectStatus ==
                                                                                  SubjectStatus.AllSeniors)).ToList());

                getSubject.AddRange(Mapper.Map<List<SubjectDto>>(subjectlist));
                var electiveSubject = ElectiveSubjectsCollections(classId, schoolId);
                getSubject.AddRange(Mapper.Map<List<SubjectDto>>(electiveSubject));
            }
            var makeEachSubjectUnique = getSubject.GroupBy(s => s.Id).Select(x => x.First()).ToList();
            if (assignedSubjects != null && assignedSubjects.Any())
            {
                //Posible subject repetation expecially in SS classes e.g. where Science Student Select Art Subject as elective. 
                var grpBySubjectId = assignedSubjects.GroupBy(c => c.SubjectId);
                var armRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
                //below gets total number of arms
                var totalNoOfArm = armRepository.Count(c => c.ClassId == classId && c.StudentDetails.Any());
                foreach (var eachSubject in grpBySubjectId)
                {
                    var totalNoOfAssignedArm = eachSubject.Count();
                    if (totalNoOfAssignedArm != 0 && totalNoOfAssignedArm == totalNoOfArm)
                    {

                        var subjectIdToRemove = eachSubject.First().SubjectId;
                        var idex = makeEachSubjectUnique.Find(f => f.Id == subjectIdToRemove);
                        makeEachSubjectUnique.Remove(idex);
                    }
                }
            }
            return
                makeEachSubjectUnique.Select(r => new SelectListItem { Text = r.SubjectName, Value = r.Id.ToString() })
                    .ToList();
        }

        public IList<SelectListItem> ArmDropDown(int selectedClassId)
        {
            var classArmRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var collections = classArmRepository.Where(a => a.ClassId == selectedClassId, a => a.Classs);
            var returnCollections =
                collections.Select(a => new SelectListItem { Text = a.ArmName.ToString(), Value = a.Id.ToString() })
                    .ToList();
            return returnCollections;
        }

        public IList<SelectListItem> ElectiveSubjectDropDown()
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var electiveSubject =
                subjectRepository.Where(
                    r =>
                        r.SubjectCategory.SubjectStatus == SubjectStatus.Elective &&
                        r.TimeStatus == Common.Shared.TimeStatus.Current &&
                        r.SubjectCategory.SchoolId == schoolId);
            return
                electiveSubject.Select(r => new SelectListItem { Text = r.SubjectName, Value = r.Id.ToString() }).ToList();
        }

        public ArmOrCategoryStudentsInTheSelectedArmDto GetCategoryStudentsInTheSelectedArm(byte catStatus, int armId,
            int subjectId)
        {
            var subjectStudentAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<SubjectStudentTxAssignment, int>>();
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var studentList = (ServiceLocator.GetList<StudentDetailsDto>()).ToList();
            var studentAssignedSubjectList = (ServiceLocator.GetList<string>()).ToList();
            if (catStatus == 1)
            {
                var studentId =
                    subjectStudentAssignmentRepository.Where(
                        s => s.StudentDetail.ClassArmId == armId && s.SubjectId == subjectId)
                        ?.Select(s => s.StudentId);
                if (studentId != null) studentAssignedSubjectList.AddRange(studentId);
                var list =
                    Mapper.Map<List<StudentDetailsDto>>(
                        studentRepository.Where(
                            s => s.ClassArmId == armId && s.Status != Common.Shared.StudentStatus.Previous, u => u.User));
                studentList.AddRange(list);
            }
            else if (catStatus == 2)
            {
                var studentId = subjectStudentAssignmentRepository.Where(
                    s =>
                        s.StudentDetail.ClassArmId == armId &&
                        s.StudentDetail.Status != Common.Shared.StudentStatus.Previous && s.SubjectId == subjectId &&
                        s.StudentDetail.ClassCategory == Common.Shared.ClassCategoryStatus.Art)?
                    .Select(r => r.StudentId);
                if (studentId != null) studentAssignedSubjectList.AddRange(studentId);
                studentList.AddRange(
                    Mapper.Map<List<StudentDetailsDto>>(
                        studentRepository.Where(
                            s =>
                                s.ClassArmId == armId && s.Status != Common.Shared.StudentStatus.Previous &&
                                s.ClassCategory == Common.Shared.ClassCategoryStatus.Art,
                            u => u.User)));
            }
            else if (catStatus == 3)
            {
                var studentId = subjectStudentAssignmentRepository.Where(
                    s =>
                        s.StudentDetail.ClassArmId == armId && s.SubjectId == subjectId &&
                        s.StudentDetail.Status != Common.Shared.StudentStatus.Previous &&
                        s.StudentDetail.ClassCategory == Common.Shared.ClassCategoryStatus.Science)?
                    .Select(a => a.StudentId);
                if (studentId != null) studentAssignedSubjectList.AddRange(studentId);
                studentList.AddRange(
                    Mapper.Map<List<StudentDetailsDto>>(
                        studentRepository.Where(
                            s =>
                                s.ClassArmId == armId && s.ClassCategory == Common.Shared.ClassCategoryStatus.Science &&
                                s.Status != Common.Shared.StudentStatus.Previous,
                            u => u.User)));
            }
            else if (catStatus == 4)
            {
                var studentId =
                    subjectStudentAssignmentRepository.Where(
                        s =>
                            s.StudentDetail.ClassArmId == armId &&
                            s.StudentDetail.Status != Common.Shared.StudentStatus.Previous && s.SubjectId == subjectId &&
                            s.StudentDetail.ClassCategory == Common.Shared.ClassCategoryStatus.Commercial)
                        ?.Select(a => a.StudentId);

                if (studentId != null) studentAssignedSubjectList.AddRange(studentId);
                studentList.AddRange(
                    Mapper.Map<List<StudentDetailsDto>>(
                        studentRepository.Where(
                            s =>
                                s.ClassArmId == armId && s.ClassCategory == Common.Shared.ClassCategoryStatus.Commercial &&
                                s.Status != Common.Shared.StudentStatus.Previous,
                            u => u.User)));
            }
            return new ArmOrCategoryStudentsInTheSelectedArmDto
            {
                StudentsList = studentList,
                AssignedStudentIds = studentAssignedSubjectList
            };
        }

        public string AssignSubjectToArmStudentsOrStudentsInCategory(List<StudentIdsDto> studentIds)
        {
            var armId = studentIds.First().Id;
            var firstIndex = studentIds.Find(s => s.Id == armId);
            studentIds.Remove(firstIndex);
            var categoriesValue = studentIds.Last();
            studentIds.Remove(categoriesValue);
            var subjectId = studentIds.First().Id;
            firstIndex = studentIds.Find(s => s.Id == subjectId);
            studentIds.Remove(firstIndex);

            var studentAssessmentRepository =
                ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();

            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var subjectStudentAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<SubjectStudentTxAssignment, int>>();
            var armId2 = int.Parse(armId);
            var subjectId2 = int.Parse(subjectId);
            var teacherSubjectArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            var classTeacherId = ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>()
                .Find(x => x.ArmId == armId2 && x.SubjectId == subjectId2)
                ?.EmployeeId;
            var teacherFullName = "";
            if (classTeacherId == null)
            {
                var classTeacherObject = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>()
                    .Where(x => x.SchoolId == schoolId && x.TimeStatus == Common.Shared.TimeStatus.Current, v => v.User)
                    .OrderBy(v => v.User.FullName).First();
                classTeacherId = classTeacherObject.Id;
                teacherFullName = classTeacherObject.User.FullName;
                var modelObject = new TeachTxerSubjectArmAssignment
                {
                    EmployeeId = (int)classTeacherId,
                    ArmId = armId2,
                    SubjectId = subjectId2
                };
                teacherSubjectArmAssignmentRepository.Insert(modelObject);
            }

            var yearlyAndTermlyStatusId = ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>().Find(
                x =>
                    x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                    x.Session.SchoolInfoId == schoolId)?.Id;

            foreach (var eachId in studentIds)
            {
                var hasSubjectPrevioulyAssigned =
                    subjectStudentAssignmentRepository.Find(
                        r =>
                            r.StudentDetail.ClassArmId == armId2 && r.StudentId == eachId.Id &&
                            r.SubjectId == subjectId2);

                var subjectStudentAssignmentModel = ServiceLocator.Get<SubjectStudentTxAssignment>();
                if (hasSubjectPrevioulyAssigned == null)
                {
                    subjectStudentAssignmentModel.StudentId = eachId.Id;
                    subjectStudentAssignmentModel.SubjectId = subjectId2;
                    subjectStudentAssignmentRepository.Insert(subjectStudentAssignmentModel);
                }
                if (yearlyAndTermlyStatusId != null)
                {
                    var currentStudentSubjectAssessmentObject =
                        studentAssessmentRepository.Find(
                            z =>
                                z.StudentId == eachId.Id && z.ClassArmId == armId2 && z.SubjectId == subjectId2 &&
                                z.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId);
                    if (currentStudentSubjectAssessmentObject == null)
                    {
                        var newAssessment = new StudenXtAccessment
                        {
                            Id = Guid.NewGuid().ToString(),
                            ClassArmId = armId2,
                            StudentId = eachId.Id,
                            EmployeeId = (int)classTeacherId,
                            SubjectId = subjectId2,
                            LastUpdateDate = DateTime.Now,
                            Ca1 = 0,
                            Ca2 = 0,
                            Ca3 = 0,
                            Exams = 0,
                            YearlyAndTermlyStatusId = (int)yearlyAndTermlyStatusId
                        };
                        studentAssessmentRepository.Insert(newAssessment);
                    }
                }
            }
            if (categoriesValue.Id == "1") //Indicate all students option
            {
                var listOfStudentThatOfferTheSubject =
                    studentAssessmentRepository.Where(
                        x =>
                            x.ClassArmId == armId2 && x.SubjectId == subjectId2 &&
                            x.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId);

                var ids = studentIds.Select(v => v.Id).ToList();
                foreach (var each in listOfStudentThatOfferTheSubject)
                {
                    if (!(ids.Contains(each.StudentId)))
                    {
                        var assignmentObject = subjectStudentAssignmentRepository.Find(
                            x => x.StudentId == each.StudentId && x.SubjectId == subjectId2);


                        studentAssessmentRepository.Delete(each);
                        subjectStudentAssignmentRepository.Delete(assignmentObject);
                    }
                }
            }
            else if (categoriesValue.Id == "2") //Indicate Art Students option
            {
                var listOfStudentThatOfferTheSubject =
                    studentAssessmentRepository.Where(
                        x =>
                            x.ClassArmId == armId2 && x.SubjectId == subjectId2 &&
                            x.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId &&
                            x.Student.ClassCategory == ClassCategoryStatus.Art, c => c.Student);

                var ids = studentIds.Select(v => v.Id).ToList();
                foreach (var each in listOfStudentThatOfferTheSubject)
                {
                    if ((!(ids.Contains(each.StudentId))) && each.Student.ClassCategory == ClassCategoryStatus.Art)
                    {
                        var assignmentObject = subjectStudentAssignmentRepository.Find(
                            x => x.StudentId == each.StudentId && x.SubjectId == subjectId2);
                        studentAssessmentRepository.Delete(each);
                        subjectStudentAssignmentRepository.Delete(assignmentObject);
                    }
                }
            }
            else if (categoriesValue.Id == "3") //Indicate science students option
            {
                var listOfStudentThatOfferTheSubject =
                    studentAssessmentRepository.Where(
                        x =>
                            x.ClassArmId == armId2 && x.SubjectId == subjectId2 &&
                            x.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId &&
                            x.Student.ClassCategory == ClassCategoryStatus.Science, c => c.Student);

                var ids = studentIds.Select(v => v.Id).ToList();
                foreach (var each in listOfStudentThatOfferTheSubject)
                {
                    if ((!(ids.Contains(each.StudentId))) && each.Student.ClassCategory == ClassCategoryStatus.Science)
                    {
                        var assignmentObject = subjectStudentAssignmentRepository.Find(
                            x => x.StudentId == each.StudentId && x.SubjectId == subjectId2);
                        studentAssessmentRepository.Delete(each);
                        subjectStudentAssignmentRepository.Delete(assignmentObject);
                    }
                }
            }
            else if (categoriesValue.Id == "4") //Indicate commercial students option
            {
                var listOfStudentThatOfferTheSubject =
                    studentAssessmentRepository.Where(
                        x =>
                            x.ClassArmId == armId2 && x.SubjectId == subjectId2 &&
                            x.YearlyAndTermlyStatusId == yearlyAndTermlyStatusId &&
                            x.Student.ClassCategory == ClassCategoryStatus.Commercial, c => c.Student);

                var ids = studentIds.Select(v => v.Id).ToList();
                foreach (var each in listOfStudentThatOfferTheSubject)
                {
                    if ((!(ids.Contains(each.StudentId))) &&
                        each.Student.ClassCategory == ClassCategoryStatus.Commercial)
                    {
                        var assignmentObject = subjectStudentAssignmentRepository.Find(
                            x => x.StudentId == each.StudentId && x.SubjectId == subjectId2);
                        studentAssessmentRepository.Delete(each);
                        subjectStudentAssignmentRepository.Delete(assignmentObject);
                    }
                }
            }
            return teacherFullName;
        }

        public bool UnAssignSubjectToArmStudentsOrStudentsInCategory(List<StudentIdsDto> studentIds)
        {
            var armId = studentIds.First().Id;
            var firstIndex = studentIds.Find(s => s.Id == armId);
            studentIds.Remove(firstIndex);
            var subjectId = studentIds.First().Id;
            firstIndex = studentIds.Find(s => s.Id == subjectId);
            studentIds.Remove(firstIndex);

            var subjectStudentAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<SubjectStudentTxAssignment, int>>();
            var armId2 = int.Parse(armId);
            var subjectId2 = int.Parse(subjectId);
            var studentAssessmentRepository =
                ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
            var armStudentAssesssment =
                studentAssessmentRepository.Where(
                    z =>
                        z.ClassArmId == armId2 && z.SubjectId == subjectId2 &&
                        z.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).ToList();

            foreach (var eachId in studentIds)
            {
                var currentTermAssessment = armStudentAssesssment.Find(x => x.StudentId == eachId.Id);

                if (currentTermAssessment != null)
                {
                    studentAssessmentRepository.Delete(currentTermAssessment);
                }
                var hasSubjectPrevioulyAssigned =
                    subjectStudentAssignmentRepository.Find(
                        r =>
                            r.StudentDetail.ClassArmId == armId2 && r.StudentId == eachId.Id &&
                            r.SubjectId == subjectId2);
                if (hasSubjectPrevioulyAssigned != null)
                {
                    subjectStudentAssignmentRepository.Delete(hasSubjectPrevioulyAssigned);
                }
            }
            return true;
        }

        public ArmOrCategoryStudentsInTheSelectedArmDto GetStudentForAnArm2Nd(int armId, int subjectId)
        {
            var subjectStudentAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<SubjectStudentTxAssignment, int>>();
            var studentIds =
                subjectStudentAssignmentRepository.Where(
                    s => s.StudentDetail.ClassArmId == armId && s.SubjectId == subjectId)?
                    .Select(r => r.StudentId)
                    .ToList();
            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var collection =
                studentDetailRepository.Where(
                    s => s.ClassArmId == armId && s.Status != Common.Shared.StudentStatus.Previous, j => j.User);
            return new ArmOrCategoryStudentsInTheSelectedArmDto
            {
                StudentsList = Mapper.Map<List<StudentDetailsDto>>(collection),
                AssignedStudentIds = studentIds
            };
        }

        public IList<SelectListItem> AccessStatu()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var status = schoolInfoRepository.Find(schoolId).ElectiveAccessStatus;
            var startStus = new List<SelectListItem>
            {
                new SelectListItem {Text = status.ToString(), Value = ((int) status).ToString()}
            };
            var enumToList = EnumsAndExtensionMethods.GetEnumToList<AccessStatus>();
            startStus.AddRange(from item in enumToList
                               where item != status
                               select new SelectListItem { Text = item.ToString(), Value = ((int)item).ToString() });
            return startStus;
        }

        public List<AgentDto> GetAgentList()
        {
            var agentRepository = ServiceLocator.Get<IGenericRepository<Agent, short>>();
            var list = agentRepository.GetAll(x => x.User);
            return list.Select(r => new AgentDto
            {
                TimeStatus = r.TimeStatus,
                UserId = r.UserId,
                User = new UserDto
                {
                    UserName = r.User.UserName,
                    AccessStatus = r.User.AccessStatus,
                    Email = r.User.Email,
                    FirstName = r.User.FirstName,
                    MiddleName = r.User.MiddleName,
                    SurName = r.User.SurName,
                    Gender = r.User.Gender,
                    PhoneNumber = r.User.PhoneNumber,
                    CreatedDate = r.User.CreatedDate,
                    Id = r.User.Id,
                    UserStatus = r.User.UserStatus
                }
            }).ToList();
        }

        public bool CreateSchoolDefaultByProprietor(string schoolName, string registrationOfficerId, string token,
            string key, string hostName)
        {
            var schoolSubjectCategoryRepository = ServiceLocator.Get<IGenericRepository<SubjectRxCategories, short>>();
            var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var schoolModel = ServiceLocator.Get<SchoolTyInfo>();
            var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            var lastRecord =
                schoolInfoRepository.Where(x => x.SchoolRegistrationOfficer.AgentUser.UserId == userId).ToList();
            short lastSerialNo = 0;
            if (lastRecord.Any())
            {
                lastSerialNo = lastRecord.Max(s => s.SsNo);
            }
            schoolModel.SchoolName = schoolName;
            lastSerialNo = (short)(lastSerialNo + 1);
            schoolModel.SsNo = lastSerialNo;
            schoolModel.SchoolRegistrationOfficerId = registrationOfficerId;
            schoolModel.SchoolName = schoolName;
            schoolModel.SchoolAdminAccountantStatus = SchoolAdminAccountantStatus.None;



            schoolModel.SystemImplementationType = FreePlugIn.Encrypt(FreePlugIn.GetUniqueSystemId() + ":0", true);
            token = !string.IsNullOrWhiteSpace(token) ? token.Trim() : null;
            schoolModel.SmsApiToken = FreePlugIn.Encrypt(token ?? Guid.NewGuid().ToString(), true);
            if (hostName == "ourschoolonline.ng" || hostName == "www.ourschoolonline.ng")
            {
                key = !string.IsNullOrWhiteSpace(key) ? key.Trim() : null;
                var arbitraryKey = Guid.NewGuid().ToString();
                schoolModel.SmsApiKey = FreePlugIn.Encrypt(key ?? arbitraryKey, true);

                var featureImpl = !string.IsNullOrWhiteSpace(key) ? key + ":0" : arbitraryKey + ":0";
                schoolModel.FeatureImplementationType = FreePlugIn.Encrypt(featureImpl, true);
            }
            else
            {
                schoolModel.SmsApiKey = FreePlugIn.Encrypt(Guid.NewGuid().ToString(), true);

                var systemId = FreePlugIn.GetUniqueSystemId();
                var implementationType = systemId + ":0";
                schoolModel.FeatureImplementationType = FreePlugIn.Encrypt(implementationType, true);
            }

            schoolInfoRepository.Insert(schoolModel);
            UnitOfWork.SaveChanges();

            var smsPaymentBalanceRepo = ServiceLocator.Get<IGenericRepository<SmsPaymentBalance, int>>();
            var smsPurchaseRepo = ServiceLocator.Get<IGenericRepository<SmsPurchase, int>>();
            var currentUser = Thread.CurrentPrincipal.Identity.GetUserId();
            var agentId = ServiceLocator.Get<IGenericRepository<Agent, short>>().Find(x => x.UserId == currentUser).Id;
            var smsPurcharseModel = new SmsPurchase
            {
                SchoolSmsId = registrationOfficerId,
                AmountPurchase = 10,
                AgentUserId = agentId,
                BalB4Purchase = 0,
                PaymentDate = DateTime.Now,
                TimeStatus = Common.Shared.TimeStatus.Current
            };
            smsPurchaseRepo.Insert(smsPurcharseModel);

            var paymentBalanceModel = new SmsPaymentBalance
            {
                Amount = 10,
                SchoolSmsId = registrationOfficerId,
                PricePerSms = 2.7f
            };
            smsPaymentBalanceRepo.Insert(paymentBalanceModel);

            var schoolId = GetSchoolIdForProprietor();
            var newSNo = (short)(schoolSubjectCategoryRepository.Count() + 1);
            var subjectCategoryList = new List<SubjectRxCategories>
            {
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.PreNursery,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Nursery,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.General,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Primary,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Jss,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.AllSeniors,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Science,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Art,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Commercial,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Elective,
                    SchoolId = schoolId,
                    Id = newSNo,
                }
            };
            foreach (var each in subjectCategoryList)
            {
                var getSubjectCategories = schoolSubjectCategoryRepository.GetAll();
                var sNo = schoolSubjectCategoryRepository.Count();
                var counter = 0;
                var subjectCategories = getSubjectCategories as IList<SubjectRxCategories> ??
                                        getSubjectCategories.ToList();
                if (subjectCategories.Any())
                {
                    var lastRecordId = subjectCategories.OrderBy(r => r.Id).Last().Id;
                    if (lastRecordId != sNo)
                    {
                        foreach (var eachSession in subjectCategories)
                        {
                            counter = counter + 1;
                            if (counter != eachSession.Id)
                            {
                                each.Id = (short)counter;
                                schoolSubjectCategoryRepository.Insert(each);
                                UnitOfWork.SaveChanges();
                                break;
                            }
                        }
                    }
                    else
                    {
                        sNo = sNo + 1;
                        each.Id = (short)sNo;
                        schoolSubjectCategoryRepository.Insert(each);
                        UnitOfWork.SaveChanges();
                    }
                }
                else
                {
                    sNo = sNo + 1;
                    each.Id = (short)sNo;
                    schoolSubjectCategoryRepository.Insert(each);
                    UnitOfWork.SaveChanges();
                }

            }
            return true;
        }

        public bool DoesThisUserNameExistForStudentUpdate(string userName, string studentId)
        {
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var user = userRepository.Find(s => s.UserName.Trim() == userName.Trim());
            if (user != null)
            {
                var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
                var studentModel =
                    studentRepository.Find(s => s.User.UserName.Trim() == userName.Trim() && s.Id == studentId);
                if (studentModel == null)
                {
                    return false; //Meaning user-name must be changed
                }
            }
            return true; //Meaning update straight away
        }

        public bool DoesThisUserNameExist(string userName)
        {
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var user = userRepository.Find(s => s.UserName.Trim() == userName.Trim());
            if (user != null)
            {
                return false;
            }
            return true;
        }

        public int GetSchoolSerialId()
        {
            var schoolRegistrationRepository = ServiceLocator.Get<IGenericRepository<SchoolRegistrationOfficer>>();
            var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            var schools = schoolRegistrationRepository.Find(s => s.UserId == userId, s => s.SchoolInfo).SchoolInfo.SsNo;
            return schools;
        }

        public bool ChangeStudentsElectiveSelectionStatus(byte? selectionId)
        {
            var status = selectionId == 0 ? AccessStatus.Active : AccessStatus.Disable;
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var schoolModel = schoolInfoRepository.Find(schoolId);
            schoolModel.ElectiveAccessStatus = status;
            schoolInfoRepository.Update(schoolModel);
            return true;
        }

        public bool DisableUserById(string id)
        {
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var userModel = userRepository.Find(i => i.Id == id);
            userModel.AccessStatus = AccessStatus.Disable;
            userRepository.Update(userModel);
            return true;
        }

        public bool EnableUserById(string id)
        {
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var userModel = userRepository.Find(i => i.Id == id);
            userModel.AccessStatus = AccessStatus.Active;
            userRepository.Update(userModel);
            return true;
        }

        public bool ChangeStudentPassword(StudentPasswordModelDto model)
        {
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var userModel = userRepository.Find(i => i.Id == model.UserId);
            var hasher = new PasswordHasher();
            userModel.PasswordHash = hasher.HashPassword(model.Password);
            userRepository.Update(userModel);
            return true;
        }

        public byte UpdateSchoolRegistrarInfo(UserDto model, string hostName)
        {
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var hasNewEmailExistAgainstAnotherUser = userRepository.Find(s => s.Email == model.Email)?.Id;
            if (hasNewEmailExistAgainstAnotherUser != null && hasNewEmailExistAgainstAnotherUser != model.Id)
            {
                return 1; //Failed! Email exist agaist another record.
            }
            var hasNewUsernameExistAgainstAnotherUser = userRepository.Find(s => s.UserName == model.UserName)?.Id;
            if (hasNewUsernameExistAgainstAnotherUser != null && hasNewUsernameExistAgainstAnotherUser != model.Id)
            {
                return 2; //Failed! Username exist agaist another user.
            }

            var schoolRegistrationOfficerRepository =
                ServiceLocator.Get<IGenericRepository<SchoolRegistrationOfficer>>();
            var schoolRegistrationOfficer = schoolRegistrationOfficerRepository.Find(i => i.UserId == model.Id,
                u => u.SchoolInfo);


            var user = userRepository.Find(model.Id);
            var securityStamp = user.SecurityStamp;

            ApplicationUser mapModel;
            if (string.IsNullOrEmpty(model.Password))
            {

                var passordHash = user.PasswordHash;
                mapModel = Mapper.Map<ApplicationUser>(model);
                mapModel.PasswordHash = passordHash;
                mapModel.SecurityStamp = securityStamp;
                mapModel.EmailConfirmed = true;
            }
            else
            {
                var hasher = new PasswordHasher();
                mapModel = Mapper.Map<ApplicationUser>(model);
                mapModel.PasswordHash = hasher.HashPassword(model.Password);
                mapModel.SecurityStamp = securityStamp;
                mapModel.EmailConfirmed = true;
            }
            userRepository.Update(mapModel);
            schoolRegistrationOfficer.TimeStatus = model.TimeStatus;
            schoolRegistrationOfficer.DateOfBirth = model.DateOfBirth;
            schoolRegistrationOfficerRepository.Update(schoolRegistrationOfficer);

            var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            model.SmsApiToken = !string.IsNullOrWhiteSpace(model.SmsApiToken) ? model.SmsApiToken.Trim() : null;
            var tokenEncode = FreePlugIn.Encrypt(model.SmsApiToken ?? Guid.NewGuid().ToString(), true);
            var isApiTokenExistOnAnotherRecord = schoolInfoRepository.Find(x => x.SmsApiToken == tokenEncode,
                c => c.SchoolRegistrationOfficer);
            if (isApiTokenExistOnAnotherRecord != null)
            {
                if (isApiTokenExistOnAnotherRecord.SchoolRegistrationOfficer.UserId != model.Id)
                {
                    return 3;
                }
            }
            var schoolModel = schoolRegistrationOfficer.SchoolInfo;

            schoolModel.SchoolName = model.SchoolName;
            schoolModel.TotalStudents = string.IsNullOrEmpty(schoolModel.TotalStudents)
                ? "0"
                : schoolModel.TotalStudents;

            var result = 0;
            if (int.TryParse(schoolModel.TotalStudents, out result))
            {
                schoolModel.TotalStudents =
                FreePlugIn.Encrypt(FreePlugIn.GetUniqueSystemId() + ":" + schoolModel.TotalStudents, true);
            }



            schoolModel.SmsApiToken = FreePlugIn.Encrypt(model.SmsApiToken ?? Guid.NewGuid().ToString(), true);
            if (hostName == "www.ourschoolonline.ng" || hostName == "ourschoolonline.ng")
            {
                model.SmsApiKey = !string.IsNullOrWhiteSpace(model.SmsApiKey) ? model.SmsApiKey.Trim() : null;
                var arbitraryKey = Guid.NewGuid().ToString();
                schoolModel.SmsApiKey = FreePlugIn.Encrypt(model.SmsApiKey ?? arbitraryKey, true);


                schoolModel.FeatureImplementationType =
                    !string.IsNullOrWhiteSpace(schoolModel.FeatureImplementationType)
                        ? schoolModel.FeatureImplementationType
                        : null;
                var featureImplType = arbitraryKey + ":0";
                schoolModel.FeatureImplementationType = schoolModel.FeatureImplementationType ??
                                                        FreePlugIn.Encrypt(featureImplType, true);
            }
            else
            {
                schoolModel.FeatureImplementationType =
                    !string.IsNullOrWhiteSpace(schoolModel.FeatureImplementationType)
                        ? schoolModel.FeatureImplementationType
                        : null;
                var systemId = FreePlugIn.GetUniqueSystemId();
                var implementationType = systemId + ":0";
                schoolModel.FeatureImplementationType = schoolModel.FeatureImplementationType ??
                                                        FreePlugIn.Encrypt(implementationType, true);
            }
            schoolInfoRepository.Update(schoolModel);

            //var userModel = schoolRegistrationOfficer.User;
            //userModel.FirstName = model.FirstName;
            //userModel.MiddleName = model.MiddleName;
            //userModel.SurName = model.SurName;
            //userModel.UserName = model.UserName;
            //userModel.PhoneNumber = model.PhoneNumber;
            //userModel.Gender = model.Gender;
            //userModel.Email = model.Email;
            return 0;
        }

        public UserDto GetSchoolRegistrarById(string userId)
        {
            var schoolRegistrationOfficerRepository =
                ServiceLocator.Get<IGenericRepository<SchoolRegistrationOfficer>>();
            var userModel = schoolRegistrationOfficerRepository.Find(i => i.UserId == userId, u => u.User,
                u => u.SchoolInfo);
            var returnUserModel = Mapper.Map<UserDto>(userModel.User);
            var key = "";
            try
            {
                key = FreePlugIn.Decrypt(userModel.SchoolInfo.SmsApiKey, true);
            }
            catch (Exception)
            {
                key = userModel.SchoolInfo.SmsApiKey;
            }

            var token = "";
            try
            {
                token = FreePlugIn.Decrypt(userModel.SchoolInfo.SmsApiToken, true);
            }
            catch (Exception)
            {
                token = userModel.SchoolInfo.SmsApiToken;
            }
            returnUserModel.SmsApiToken = token;
            returnUserModel.SmsApiKey = key;
            returnUserModel.Addresses = userModel.FullAddress;
            returnUserModel.SchoolName = userModel.SchoolInfo.SchoolName;
            returnUserModel.DateOfBirth = userModel.DateOfBirth;
            return returnUserModel;
        }

        public SchoolInfoDto GetSchoolRegistrarDetailsById(string userId)
        {
            var schoolRegistrationOfficerRepository =
                ServiceLocator.Get<IGenericRepository<SchoolRegistrationOfficer>>();
            var schoolRegistrationOfficerModel =
                schoolRegistrationOfficerRepository.Find(i => i.UserId == userId, u => u.SchoolInfo).SchoolInfo;
            return Mapper.Map<SchoolInfoDto>(schoolRegistrationOfficerModel);
        }

        public byte DeleteSchoolRegistrar(string userId)
        {
            var studentDetailsRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var hasHeRegisteredStudent =
                studentDetailsRepository.Find(a => a.SchoolInfo.SchoolRegistrationOfficer.UserId == userId);
            if (hasHeRegisteredStudent != null)
            {
                return 1;
                //Failed! The Registrar has registered student(s) and therefore we cannot DELETE the Registrar.
            }
            var classsRepository = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var hasHeclasses = classsRepository.Find(a => a.SchoolInfo.SchoolRegistrationOfficer.UserId == userId);
            if (hasHeclasses != null)
            {
                return 2; //Failed! The Registrar has registered Class(es) and therefore we cannot DELETE the Registrar.
            }
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var hasHeSubject =
                subjectRepository.Find(a => a.SubjectCategory.SchoolInfo.SchoolRegistrationOfficer.UserId == userId);
            if (hasHeSubject != null)
            {
                return 3;
                //Failed! The Registrar has registered Subject(s) and therefore we cannot DELETE the Registrar.
            }
            var smsPurchaseRepository = ServiceLocator.Get<IGenericRepository<SmsPurchase, int>>();
            var purchaseList =
                smsPurchaseRepository.Where(d => d.SchoolSmsInfo.SchoolRegistrationOfficer.UserId == userId).ToList();

            var usedSms =
                ServiceLocator.Get<IGenericRepository<SmsUsed, int>>()
                    .Find(d => d.SchoolSmsInfo.SchoolRegistrationOfficer.UserId == userId);
            if (usedSms != null)
            {
                return 4;
            }
            else
            {
                var smsPaymentBalanceRepository = ServiceLocator.Get<IGenericRepository<SmsPaymentBalance, int>>();
                foreach (var each in purchaseList)
                {
                    smsPurchaseRepository.Delete(each);
                }

                var smsBal =
                    smsPaymentBalanceRepository.Find(c => c.SchoolSmsInfo.SchoolRegistrationOfficer.UserId == userId);
                smsPaymentBalanceRepository.Delete(smsBal);
            }

            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var schoolRegistrationOfficerRepository =
                ServiceLocator.Get<IGenericRepository<SchoolRegistrationOfficer>>();
            var subjectCategoriesRepository = ServiceLocator.Get<IGenericRepository<SubjectRxCategories, short>>();
            var subjectCategories =
                subjectCategoriesRepository.Where(k => k.SchoolInfo.SchoolRegistrationOfficer.UserId == userId,
                    c => c.SchoolInfo).ToList();
            foreach (var each in subjectCategories)
            {
                subjectCategoriesRepository.Delete(each);
            }
            var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var schoolModel = schoolInfoRepository.Find(x => x.SchoolRegistrationOfficer.UserId == userId);
            schoolInfoRepository.Delete(schoolModel);

            var registrationOfficerModel = schoolRegistrationOfficerRepository.Find(s => s.UserId == userId, o => o.User);
            var userModel = registrationOfficerModel.User;
            schoolRegistrationOfficerRepository.Delete(registrationOfficerModel);
            userRepository.Delete(userModel);
            return 0;
        }

        public IEnumerable<SchoolInfoDto> GetAllRegistrarForSuperAdmin()
        {
            var schoolRegistrationOfficerRepository =
                ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            return
                Mapper.Map<IEnumerable<SchoolInfoDto>>(
                    schoolRegistrationOfficerRepository.GetAll(s => s.SchoolRegistrationOfficer.AgentUser,
                        s => s.SchoolRegistrationOfficer.User, s => s.SchoolRegistrationOfficer.SchoolInfo));
        }

        public IEnumerable<SchoolInfoDto> GetAllRegistrarForAgents()
        {
            var schoolRegistrationOfficerRepository =
                ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            return
                Mapper.Map<IEnumerable<SchoolInfoDto>>(
                    schoolRegistrationOfficerRepository.Where(
                        x => x.SchoolRegistrationOfficer.AgentUser.UserId == userId,
                        s => s.SchoolRegistrationOfficer.AgentUser,
                        s => s.SchoolRegistrationOfficer.User, s => s.SchoolRegistrationOfficer.SchoolInfo));
        }

        public IList<SelectListItem> GetSessionsDropdown()
        {
            var academicSessionRepository = ServiceLocator.Get<IGenericRepository<TxAcademicSession, short>>();
            var schoolId = "";
            if (Thread.CurrentPrincipal.IsInRole("Registry"))
            {
                schoolId = GetSchoolIdForSchoolRegistrationOfficer(); //
            }
            else if (Thread.CurrentPrincipal.IsInRole("Teacher"))
            {
                schoolId = CurrentTeacherModel().SchoolId; //
            }
            else if (Thread.CurrentPrincipal.IsInRole("Accounting"))
            {
                schoolId = CurrentTeacherModel().SchoolId; //Accounting
            }
            else
            {
                var userId = Thread.CurrentPrincipal.Identity.GetUserId();
                schoolId =
                    ServiceLocator.Get<IGenericRepository<TxStudentDetails>>()
                        .Find(s => s.UserId == userId)
                        .SchoolInfoId;
            }

            var sessions = academicSessionRepository.Where(r => r.SchoolInfoId == schoolId);
            return sessions.Select(r => new SelectListItem { Text = r.Session, Value = r.Id.ToString() }).ToList();
        }

        public IList<SelectListItem> GetTermsDropdown()
        {
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var sessions =
                yearlyAndTermlyAssessmentStatusRepository.Where(r => r.Session.SchoolInfoId == schoolId)
                    .GroupBy(f => f.TermStatus)
                    .Select(v => v.First());
            return
                sessions.Select(
                    r => new SelectListItem { Text = r.TermStatus.ToString(), Value = ((int)r.TermStatus).ToString() })
                    .ToList();
        }

        public IEnumerable<SelectListItem> HeadStatu(HeadStatus headStatus)
        {
            var gender = new List<SelectListItem>
            {
                new SelectListItem {Text = headStatus.ToString(), Value = ((int) headStatus).ToString()}
            };
            var enumToList = EnumsAndExtensionMethods.GetEnumToList<HeadStatus>();
            gender.AddRange(from item in enumToList
                            where item != headStatus
                            select new SelectListItem { Text = item.ToString(), Value = ((int)item).ToString() });
            return gender;
        }

        public byte CreateSchoolHead()
        {
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();

            var schoolHeadRepository = ServiceLocator.Get<IGenericRepository<SchoolHeadTx, short>>();
            var currentSchoolHead =
                schoolHeadRepository.Where(
                    f => f.SchoolInfoId == schoolId && f.Status == Common.Shared.TimeStatus.Current,
                    b => b.SchoolHeadCategory)?.ToList();
            if (currentSchoolHead != null && currentSchoolHead.Any())
            {
                var headMasterId =
                    currentSchoolHead.Find(f => f.SchoolHeadCategory.HeadStatus == HeadStatus.HeadTeacher)?.Id;
                var principalId =
                    currentSchoolHead.Find(f => f.SchoolHeadCategory.HeadStatus == HeadStatus.Principal)?.Id;
                var yearlyAndTermlyAssessmentStatusModel =
                    yearlyAndTermlyAssessmentStatusRepository.Find(
                        f => f.Session.SchoolInfoId == schoolId &&
                             (f.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                              f.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing));

                if (yearlyAndTermlyAssessmentStatusModel != null)
                {
                    yearlyAndTermlyAssessmentStatusModel.HeadmasterId = headMasterId;
                    yearlyAndTermlyAssessmentStatusModel.PrincipalId = principalId;
                    yearlyAndTermlyAssessmentStatusRepository.Update(yearlyAndTermlyAssessmentStatusModel);
                    return 0;
                }
            }
            return 1;
        }

        public string GetSchoolHeadNames()
        {
            var schoolHeadRepository = ServiceLocator.Get<IGenericRepository<SchoolHeadTx, short>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();

            var yearlyAndTermlyAssessmentStatusModel =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    f => f.Session.SchoolInfoId == schoolId &&
                         (f.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                          f.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing));

            if (yearlyAndTermlyAssessmentStatusModel != null)
            {
                var headMasterId = yearlyAndTermlyAssessmentStatusModel.HeadmasterId;
                var principalId = yearlyAndTermlyAssessmentStatusModel.PrincipalId;

                var headMasterName =
                    schoolHeadRepository.Find(
                        f => f.SchoolInfoId == schoolId && f.Id == headMasterId)?.HeadsName;
                var principalName =
                    schoolHeadRepository.Find(
                        f => f.SchoolInfoId == schoolId && f.Id == principalId)?.HeadsName;
                return "Headmaster's name is: " + (headMasterName ?? "No Headmaster") + " while Principal's name is: " +
                       (principalName ?? "No Principal");
            }
            return "Sorry! You have not yet set school head.";
        }

        public NumberOfCasSettingDto GetCurrentCasSettings()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();

            var yearlyAndTermlyAssessmentStatusModel =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    f => f.Session.SchoolInfoId == schoolId &&
                         (f.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                          f.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing));
            var numberOfCasList = new List<SelectListItem>();
            if (yearlyAndTermlyAssessmentStatusModel != null)
            {
                var numberOfCas = yearlyAndTermlyAssessmentStatusModel.CasCategory;
                numberOfCasList.Add(new SelectListItem
                {
                    Text = yearlyAndTermlyAssessmentStatusModel.CasCategory.ToString(),
                    Value = ((int)yearlyAndTermlyAssessmentStatusModel.CasCategory).ToString()
                });
                var enumToList = EnumsAndExtensionMethods.GetEnumToList<CasCategory>();
                numberOfCasList.AddRange(from item in enumToList
                                         where item != yearlyAndTermlyAssessmentStatusModel.CasCategory
                                         select new SelectListItem { Text = item.ToString(), Value = ((int)item).ToString() });
                return new NumberOfCasSettingDto
                {
                    NumberOfCasCategoyDropdown = numberOfCasList,
                    PerCasValue = yearlyAndTermlyAssessmentStatusModel.PerCasValue
                };
            }
            else
            {
                var enumToList = EnumsAndExtensionMethods.GetEnumToList<CasCategory>();
                numberOfCasList.AddRange(
                    enumToList.Select(r => new SelectListItem { Text = r.ToString(), Value = ((int)r).ToString() }));
            }
            return new NumberOfCasSettingDto();
        }

        public bool SubmitCaCategorySetting(CasSettingDto model)
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();

            var yearlyAndTermlyAssessmentStatusModel =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    f => f.Session.SchoolInfoId == schoolId &&
                         (f.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                          f.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing));
            if (yearlyAndTermlyAssessmentStatusModel != null)
            {
                yearlyAndTermlyAssessmentStatusModel.CasCategory = model.CasCategory;
                yearlyAndTermlyAssessmentStatusModel.PerCasValue = model.PerCasValue;
                yearlyAndTermlyAssessmentStatusRepository.Update(yearlyAndTermlyAssessmentStatusModel);

                var continuousAssessmentTypeRepository = ServiceLocator.Get<IGenericRepository<ContinuousAssessmentType, short>>();
                var isEverIserted = continuousAssessmentTypeRepository.Where(x => x.SchoolId == schoolId).ToList();
                if (model.CasCategory == CasCategory.OneCA)
                {
                    var firstOneId = isEverIserted.FirstOrDefault(a => a.AssessmentType == AssessmentType.FirstCA);
                    if (firstOneId == null)
                    {
                        var model1 = new ContinuousAssessmentType
                        {
                            SchoolId = schoolId,
                            AssessmentType = AssessmentType.FirstCA
                        };
                        continuousAssessmentTypeRepository.Insert(model1);
                    }
                    var others = isEverIserted.Where(a => a.AssessmentType != AssessmentType.FirstCA).ToList();
                    if (others.Any())
                    {
                        foreach (var each in others)
                        {
                            continuousAssessmentTypeRepository.Delete(each);
                        }
                    }
                }
                else if (model.CasCategory == CasCategory.TwoCAs)
                {
                    var firstOneId = isEverIserted.FirstOrDefault(a => a.AssessmentType == AssessmentType.FirstCA);
                    if (firstOneId == null)
                    {
                        var model01 = new ContinuousAssessmentType
                        {
                            SchoolId = schoolId,
                            AssessmentType = AssessmentType.FirstCA
                        };
                        continuousAssessmentTypeRepository.Insert(model01);
                    }
                    var secondOne = isEverIserted.FirstOrDefault(a => a.AssessmentType == AssessmentType.SecondCA);
                    if (secondOne == null)
                    {
                        var model02 = new ContinuousAssessmentType
                        {
                            SchoolId = schoolId,
                            AssessmentType = AssessmentType.SecondCA
                        };
                        continuousAssessmentTypeRepository.Insert(model02);
                    }

                    var thirdOne = isEverIserted.FirstOrDefault(a => a.AssessmentType == AssessmentType.ThirdCA);
                    if (thirdOne != null)
                    {
                        continuousAssessmentTypeRepository.Delete(thirdOne);
                    }
                }
                else if (model.CasCategory == CasCategory.ThreeCAs)
                {
                    var firstOneId = isEverIserted.FirstOrDefault(a => a.AssessmentType == AssessmentType.FirstCA);
                    if (firstOneId == null)
                    {
                        var model01 = new ContinuousAssessmentType
                        {
                            SchoolId = schoolId,
                            AssessmentType = AssessmentType.FirstCA
                        };
                        continuousAssessmentTypeRepository.Insert(model01);
                    }
                    var secondOne = isEverIserted.FirstOrDefault(a => a.AssessmentType == AssessmentType.SecondCA);
                    if (secondOne == null)
                    {
                        var model02 = new ContinuousAssessmentType
                        {
                            SchoolId = schoolId,
                            AssessmentType = AssessmentType.SecondCA
                        };
                        continuousAssessmentTypeRepository.Insert(model02);
                    }

                    var thirdOne = isEverIserted.FirstOrDefault(a => a.AssessmentType == AssessmentType.ThirdCA);
                    if (thirdOne == null)
                    {
                        var model03 = new ContinuousAssessmentType
                        {
                            SchoolId = schoolId,
                            AssessmentType = AssessmentType.ThirdCA
                        };
                        continuousAssessmentTypeRepository.Insert(model03);
                    }
                }
                return true;
            }           
            return false;
        }

        public bool SubmitPassMark(byte? passMark)
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();

            var yearlyAndTermlyAssessmentStatusModel =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    f => f.Session.SchoolInfoId == schoolId &&
                         (f.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                          f.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing));
            if (yearlyAndTermlyAssessmentStatusModel == null)
            {
                return false;
            }
            yearlyAndTermlyAssessmentStatusModel.MinPassMark = passMark;
            yearlyAndTermlyAssessmentStatusRepository.Update(yearlyAndTermlyAssessmentStatusModel);
            return true;
        }

        public byte? GetPassMark()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();

            var yearlyAndTermlyAssessmentStatusModel =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    f => f.Session.SchoolInfoId == schoolId &&
                         (f.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                          f.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing));
            return yearlyAndTermlyAssessmentStatusModel?.MinPassMark;
        }

       

        public List<EmployeeDto> GetAllTeacher(string schoolId)
        {
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            return
                Mapper.Map<List<EmployeeDto>>(teacherRepository.Where(x => x.SchoolId == schoolId, s => s.User).ToList());
        }

        public List<StudentDetailsDto> GetAllStudents(string schoolId)
        {
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            return
                Mapper.Map<List<StudentDetailsDto>>(
                    studentRepository.Where(
                        x => x.SchoolInfoId == schoolId && x.Status != Common.Shared.StudentStatus.Previous, s => s.User,
                        c => c.ClassArm.Classs).ToList());
        }

        public SchoolSummaryDto GetSchoolSummaryForRegistrarDashboard()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            //var noOfTeachers =erRep
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var teachers =
                teacherRepository.Count(
                    t =>
                        t.SchoolId == schoolId &&
                        t.TimeStatus == Common.Shared.TimeStatus.Current);
            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();

            var currentStudents = studentDetailRepository.Where(t =>
                t.SchoolInfoId == schoolId &&
                (t.Status == Common.Shared.StudentStatus.Fresher || t.Status == Common.Shared.StudentStatus.Returning),
                c => c.ClassArm.Classs).ToList();
            var groupByClass =
                currentStudents.OrderBy(c => c.ClassArm.Classs.ClassName).GroupBy(f => f.ClassArm.Classs.ClassName);

            var students = currentStudents.Count();

            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var yearlyAndTermlyAssessmentStatus =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    f =>
                        f.Session.SchoolInfoId == schoolId && f.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                    s => s.Session);
            if (yearlyAndTermlyAssessmentStatus == null)
            {
                return null;
            }
            var classAndStudentInaClass = new List<DashboardStudentsNumberAnalysisDto>();
            foreach (var eachClass in groupByClass)
            {
                classAndStudentInaClass.Add(new DashboardStudentsNumberAnalysisDto
                {
                    ClassLabel = eachClass.First().ClassArm.Classs.ClassLabel,
                    NumberOfStudents = (short)eachClass.Count()
                });
            }
            var session = yearlyAndTermlyAssessmentStatus.Session.Session;
            var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();

            var currentTerm = yearlyAndTermlyAssessmentStatus.TermStatus;

            List<StudenXtAccessment> assessmentList;
            if (currentTerm == TermStatus.Third)
            {
                assessmentList = studentAccessmentRepository.Where(
                    s =>
                        s.YearlyAndTermlyStatus.SessionId == yearlyAndTermlyAssessmentStatus.SessionId,
                    d => d.ClassArm.Classs, g => g.Student.User)?.ToList();
            }
            else
            {
                assessmentList =
                    studentAccessmentRepository.Where(
                        s =>
                            s.YearlyAndTermlyStatusId == yearlyAndTermlyAssessmentStatus.Id, d => d.ClassArm.Classs,
                        g => g.Student.User)?.ToList();
            }

            if (assessmentList != null)
            {
                var assessmentOrderGrp =
                    assessmentList.OrderBy(d => d.ClassArm.Classs.ClassName).GroupBy(c => c.ClassArm.ClassId);

                var bestStudents = new List<StudentsAccessmentDto>();
                foreach (var eachclass in assessmentOrderGrp)
                {
                    var eachClassThreeBestStudent =
                        eachclass.ToList()
                            .GroupBy(s => s.StudentId)
                            .OrderByDescending(d => (d.Sum(e => e.Ca1 + e.Ca2 + e.Ca3 + e.Exams)) / d.Count())
                            .Take(3);
                    foreach (var eachItem in eachClassThreeBestStudent)
                    {
                        var eachItemToList = eachItem.ToList();
                        bestStudents.AddRange(Mapper.Map<List<StudentsAccessmentDto>>(eachItemToList));
                    }

                }
                return new SchoolSummaryDto
                {
                    Session = session,
                    Term = currentTerm.ToString(),
                    TotalStudents = students,
                    TotalTeachers = teachers,
                    StudentsAccessment = bestStudents,
                    DashboardStudentsNumberAnalysis = classAndStudentInaClass
                };
            }
            return null;
        }

        private double GetFeesForReturningStudent(SetSchoolFeesTr feesModel, ClassCategoryStatus classStatus)
        {
            double feesValue = 0;
            if (classStatus == ClassCategoryStatus.PlayClass)
            {
                feesValue = (double)feesModel.RetnPlayClass;
            }
            else if (classStatus == ClassCategoryStatus.PreNursery)
            {
                feesValue = (double)feesModel.RetnPreNursery;
            }
            else if (classStatus == ClassCategoryStatus.Nursery)
            {
                feesValue = (double)feesModel.RetnNursery;
            }
            else if (classStatus == ClassCategoryStatus.Primary)
            {
                feesValue = (double)feesModel.RetnPrimary;
            }
            else if (classStatus == ClassCategoryStatus.Jss)
            {
                feesValue = (double)feesModel.RetnJss;
            }
            else if (classStatus == ClassCategoryStatus.Science)
            {
                feesValue = (double)feesModel.RetnScience;
            }
            else if (classStatus == ClassCategoryStatus.Art)
            {
                feesValue = (double)feesModel.RetnArt;
            }
            else if (classStatus == ClassCategoryStatus.Commercial)
            {
                feesValue = (double)feesModel.RetnCommercial;
            }
            return feesValue;
        }

        private double GetFeesForFreshiers(SetSchoolFeesTr feesModel, ClassCategoryStatus classStatus)
        {
            double feesValue = 0;
            if (classStatus == ClassCategoryStatus.PlayClass)
            {
                feesValue = (double)feesModel.FreshPlayClass;
            }
            else if (classStatus == ClassCategoryStatus.PreNursery)
            {
                feesValue = (double)feesModel.FreshPreNursery;
            }
            else if (classStatus == ClassCategoryStatus.Nursery)
            {
                feesValue = (double)feesModel.FreshNursery;
            }
            else if (classStatus == ClassCategoryStatus.Primary)
            {
                feesValue = (double)feesModel.FreshPrimary;
            }
            else if (classStatus == ClassCategoryStatus.Jss)
            {
                feesValue = (double)feesModel.FreshJss;
            }
            else if (classStatus == ClassCategoryStatus.Science)
            {
                feesValue = (double)feesModel.FreshScience;
            }
            else if (classStatus == ClassCategoryStatus.Art)
            {
                feesValue = (double)feesModel.FreshArt;
            }
            else if (classStatus == ClassCategoryStatus.Commercial)
            {
                feesValue = (double)feesModel.FreshCommercial;
            }
            return feesValue;
        }

        public IList<StudentFieldExtraction4AccountingDto> GetReturningStudentsList()
        {
            var schoolFeesPaymentRepository = ServiceLocator.Get<IGenericRepository<SetSchoolFeesTr, int>>();
            var schoolId = CurrentTeacherModel().SchoolId;
            var feesPricesList =
                schoolFeesPaymentRepository.Where(
                    d =>
                        d.SchoolId == schoolId, c => c.YearlyAndTermlyStatus).ToList();


            if (!feesPricesList.Any()) //else, 
            {
                return null; //School Fees needs to be set first
            }

            var currentFeeModel =
                feesPricesList.Find(
                    s => s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current);
            var studentFieldExtractionList = new List<StudentFieldExtraction4AccountingDto>();
            if (currentFeeModel == null)
            {
                //You need to re-save the School Fees
                var lastFeesModel =
                    feesPricesList.OrderByDescending(x => x.YearlyAndTermlyStatusId).ToList().First();

                var yearlyAndTermlyStatusRepository =
                    ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
                //var setSchoolFeesRepository = ServiceLocator.Get<IGenericRepository<SetSchoolFeesTr, int>>();
                var yearlyAndTermlyAssessmentStatusId =
                    yearlyAndTermlyStatusRepository.Find(
                        s =>
                            s.Session.SchoolInfoId == schoolId &&
                            s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).Id;

                lastFeesModel.YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId;
                schoolFeesPaymentRepository.Insert(lastFeesModel);
                UnitOfWork.SaveChanges();
            }
            //List<StudentAcademicTxProgressHistory> returningStudent;
            var studentHistoryRepository =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
            var returningStudentInAcademicProgressHistory = studentHistoryRepository.Where(
                x =>
                    x.StudentDetails.SchoolInfoId == schoolId &&
                   x.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                    x.StudentDetails.Status == Common.Shared.StudentStatus.Returning,
                s => s.StudentDetails.User, x => x.StudentDetails.ClassArm,
                a => a.Classs)
                .ToList();


            var actualNumberOfReturningStudents =
                    ServiceLocator.Get<IGenericRepository<TxStudentDetails>>().Count(
                    s =>
                        s.SchoolInfoId == schoolId &&
                        s.Status == Common.Shared.StudentStatus.Returning);

            if(returningStudentInAcademicProgressHistory.Count() != actualNumberOfReturningStudents)
            {
                var studentRepo = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
                var actualReturningStudents = studentRepo.Where(
                    s =>
                        s.SchoolInfoId == schoolId &&
                        s.Status == Common.Shared.StudentStatus.Returning,
                        s=> s.ClassArm).ToList();

                foreach (var item in actualReturningStudents)
                {
                    if(returningStudentInAcademicProgressHistory.Find(x=> x.StudentId == item.Id) == null)
                    {
                        var progress = new StudentAcademicTxProgressHistory
                        {
                            StudentId = item.Id,
                            ClassId = item.ClassArm.ClassId,
                            YearlyAndTermlyStatusId = returningStudentInAcademicProgressHistory.First().YearlyAndTermlyStatusId,
                            ClassCategory = item.ClassCategory
                        };
                        studentHistoryRepository.Insert(progress);
                    }
                }
                UnitOfWork.Commit();
            }

            if (returningStudentInAcademicProgressHistory.Any())
            {
                foreach (var eachStudentHistory in returningStudentInAcademicProgressHistory)
                {
                    var currentFeePaymentStatus = FeesPaymentStatus.Completed;
                    var previousFeePaymentStatus = FeesPaymentStatus.Completed;
                    var studentModel = eachStudentHistory.StudentDetails;

                    var termIdOfStartUsingAccountingFeature =
                        ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>()
                            .Find(z => z.SchoolRegistrationOfficerId == schoolId)
                            .AccountingStartTermId;

                    var listOfAcademicProgressSoFar = studentHistoryRepository
                        .Where(c =>
                            c.YearlyAndTermlyStatusId >=
                            termIdOfStartUsingAccountingFeature && c.StudentId == studentModel.Id &&
                            c.YearlyAndTermlyStatusId >= studentModel.AdmisnTermId, x => x.Classs,
                            v => v.StudentTermlyFeePayment)
                        .ToList();
                    var noOfAcademicProgress = listOfAcademicProgressSoFar.Count();

                    var initialFeePaymentObject =
                        listOfAcademicProgressSoFar.OrderBy(d => d.YearlyAndTermlyStatusId).First();
                    var initialTermId = initialFeePaymentObject.YearlyAndTermlyStatusId;

                    var actualFeeModel =
                        schoolFeesPaymentRepository.Find(x => x.YearlyAndTermlyStatusId == initialTermId);

                    double initialFeeActualPayment = 0;
                    var studentInitialRegStatus =
                        listOfAcademicProgressSoFar.First().StudentDetails.StudentInitialRegStatus;
                    var classss = listOfAcademicProgressSoFar.First().Classs.ClassName;

                    ClassCategoryStatus actualClassCategory;
                    if (studentInitialRegStatus == StudentInitialRegistrationStatus.Fresher)
                    {
                        if (!(classss == CurrentClass.Ss1 || classss == CurrentClass.Ss2 || classss == CurrentClass.Ss3))
                        {
                            var className3 =
                                GetClassCategoryStatus(listOfAcademicProgressSoFar.First().Classs.ClassName);
                            {
                                initialFeeActualPayment = GetFeesForFreshiers(actualFeeModel, className3);
                                actualClassCategory =
                                    GetClassCategoryStatus(eachStudentHistory.Classs.ClassName);
                                // To avoid Mistake
                            }
                        }
                        else
                        {
                            initialFeeActualPayment = GetFeesForFreshiers(actualFeeModel,
                                listOfAcademicProgressSoFar.First().StudentDetails.ClassCategory);
                            actualClassCategory = eachStudentHistory.StudentDetails.ClassCategory;
                        }
                    }
                    else
                    {
                        if (!(classss == CurrentClass.Ss1 || classss == CurrentClass.Ss2 || classss == CurrentClass.Ss3))
                        {
                            var className3 =
                                GetClassCategoryStatus(listOfAcademicProgressSoFar.First().Classs.ClassName);
                            {
                                initialFeeActualPayment = GetFeesForReturningStudent(actualFeeModel, className3);
                                actualClassCategory =
                                    GetClassCategoryStatus(eachStudentHistory.Classs.ClassName);
                                // To avoid Mistake
                            }
                        }
                        else
                        {
                            initialFeeActualPayment = GetFeesForReturningStudent(actualFeeModel,
                                listOfAcademicProgressSoFar.First().StudentDetails.ClassCategory);
                            actualClassCategory = eachStudentHistory.StudentDetails.ClassCategory;
                        }
                    }

                    var totalPayment = listOfAcademicProgressSoFar.First().StudentTermlyFeePayment.Sum(x => x.Amount);
                    if (totalPayment < initialFeeActualPayment)
                    {
                        var studentHistoryId = listOfAcademicProgressSoFar.First().Id;
                        var feePaymentDiscount =
                            ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPaymentDiscount, short>>()
                                .Find(z => z.StudentAcademicProgressId == studentHistoryId);
                        if (noOfAcademicProgress == 1)
                        {
                            if (feePaymentDiscount != null)
                            {
                                currentFeePaymentStatus = FeesPaymentStatus.Completed;
                            }
                            else if (totalPayment > 0)
                            {
                                currentFeePaymentStatus = FeesPaymentStatus.PartPayment;
                            }
                            else if (Math.Abs(totalPayment) < 0.9)
                            {
                                currentFeePaymentStatus = FeesPaymentStatus.None;
                            }
                        }
                        else
                        {
                            if (feePaymentDiscount != null)
                            {
                                previousFeePaymentStatus = FeesPaymentStatus.Completed;
                            }
                            else if (totalPayment > 0)
                            {
                                previousFeePaymentStatus = FeesPaymentStatus.PartPayment;
                            }
                            else if (Math.Abs(totalPayment) < 0.9)
                            {
                                previousFeePaymentStatus = FeesPaymentStatus.None;
                            }
                        }

                    }
                    listOfAcademicProgressSoFar.Remove(listOfAcademicProgressSoFar.First());

                    if (listOfAcademicProgressSoFar.Any())
                    {
                        var currentAcademicHistoryPaymentStatus = listOfAcademicProgressSoFar.OrderBy(x => x.Id).Last();
                        var returningStudentFee = 0.0;
                        actualFeeModel =
                            schoolFeesPaymentRepository.Find(
                                x =>
                                    x.YearlyAndTermlyStatusId ==
                                    currentAcademicHistoryPaymentStatus.YearlyAndTermlyStatusId);
                        if (
                            !(classss == CurrentClass.Ss1 || classss == CurrentClass.Ss2 ||
                              classss == CurrentClass.Ss3))
                        {
                            var classCategory =
                                GetClassCategoryStatus(listOfAcademicProgressSoFar.First().Classs.ClassName);
                            {
                                returningStudentFee = GetFeesForReturningStudent(actualFeeModel, classCategory);
                                // To avoid Mistake
                            }
                        }
                        else
                        {
                            returningStudentFee = GetFeesForReturningStudent(actualFeeModel,
                                currentAcademicHistoryPaymentStatus.StudentDetails.ClassCategory);
                        }


                        var totalPartPayment =
                            currentAcademicHistoryPaymentStatus.StudentTermlyFeePayment.Sum(x => x.Amount);
                        if (Math.Abs(totalPartPayment - returningStudentFee) < 0.7)
                        {
                            currentFeePaymentStatus = FeesPaymentStatus.Completed;
                        }
                        else if (totalPartPayment >= 0 && totalPartPayment < returningStudentFee)
                        {
                            var studentAcademicProgressId = studentHistoryRepository.Find(
                                x =>
                                    x.StudentId == eachStudentHistory.StudentId &&
                                    x.YearlyAndTermlyStatus.TermlyAccessmentStatus ==
                                    TermlyAccessmentStatus.Current).Id;

                            var hasAnyDiscountThisTerm =
                                ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPaymentDiscount, short>>()
                                    .Find(v => v.StudentAcademicProgressId == studentAcademicProgressId);
                            if (hasAnyDiscountThisTerm == null && totalPartPayment > 0) //
                            {
                                currentFeePaymentStatus = FeesPaymentStatus.PartPayment;
                            }
                            else if (hasAnyDiscountThisTerm == null && Math.Abs(totalPartPayment) < 0.5) //
                            {
                                currentFeePaymentStatus = FeesPaymentStatus.None;
                            }
                            else if (
                                Math.Abs((hasAnyDiscountThisTerm?.DiscountAmount ?? 0 + totalPartPayment) -
                                         returningStudentFee) <
                                0.9) //Confirm completion
                            {
                                currentFeePaymentStatus = FeesPaymentStatus.Completed;
                            }
                            listOfAcademicProgressSoFar.Remove(currentAcademicHistoryPaymentStatus);
                        }

                        if (listOfAcademicProgressSoFar.Any() && previousFeePaymentStatus == FeesPaymentStatus.Completed)
                        {

                            foreach (var each in listOfAcademicProgressSoFar)
                            {
                                actualFeeModel =
                                    schoolFeesPaymentRepository.Find(
                                        x => x.YearlyAndTermlyStatusId == each.YearlyAndTermlyStatusId);
                                if (
                                    !(classss == CurrentClass.Ss1 || classss == CurrentClass.Ss2 ||
                                      classss == CurrentClass.Ss3))
                                {
                                    var classCategory =
                                        GetClassCategoryStatus(listOfAcademicProgressSoFar.First().Classs.ClassName);
                                    {
                                        returningStudentFee = GetFeesForReturningStudent(actualFeeModel, classCategory);
                                        // To avoid Mistake
                                    }
                                }
                                else
                                {
                                    returningStudentFee = GetFeesForReturningStudent(actualFeeModel,
                                        each.StudentDetails.ClassCategory);
                                }


                                totalPartPayment = each.StudentTermlyFeePayment.Sum(x => x.Amount);
                                if (Math.Abs(totalPartPayment - returningStudentFee) < 0.7)
                                {
                                    previousFeePaymentStatus = FeesPaymentStatus.Completed;
                                }
                                else if (totalPartPayment >= 0 && totalPartPayment < returningStudentFee)
                                {
                                    var studentAcademicProgressId = studentHistoryRepository.Find(
                                        x =>
                                            x.StudentId == eachStudentHistory.StudentId &&
                                            x.YearlyAndTermlyStatus.TermlyAccessmentStatus ==
                                            TermlyAccessmentStatus.Current).Id;

                                    var hasAnyDiscountThisTerm =
                                        ServiceLocator
                                            .Get<IGenericRepository<StudentTyTermlyFixedPaymentDiscount, short>>()
                                            .Find(v => v.StudentAcademicProgressId == studentAcademicProgressId);
                                    if (hasAnyDiscountThisTerm == null && totalPartPayment > 0) //
                                    {
                                        previousFeePaymentStatus = FeesPaymentStatus.PartPayment;
                                        break;
                                    }
                                    if (hasAnyDiscountThisTerm == null && Math.Abs(totalPartPayment) < 0.5) //
                                    {
                                        previousFeePaymentStatus = FeesPaymentStatus.None;
                                        break;
                                    }
                                    if (
                                        Math.Abs((hasAnyDiscountThisTerm?.DiscountAmount ?? 0 + totalPartPayment) -
                                                 returningStudentFee) < 0.9) //Confirm completion
                                    {
                                        previousFeePaymentStatus = FeesPaymentStatus.Completed;
                                    }
                                }
                                else
                                {
                                    previousFeePaymentStatus = FeesPaymentStatus.Completed;
                                }

                            }
                        }

                    }
                    else
                    {
                        previousFeePaymentStatus = FeesPaymentStatus.Completed;
                    }

                    var otherPaymentStatus = previousFeePaymentStatus;

                    if (otherPaymentStatus != FeesPaymentStatus.Completed ||
                        currentFeePaymentStatus != FeesPaymentStatus.Completed)
                    {
                        studentFieldExtractionList.Add(new StudentFieldExtraction4AccountingDto
                        {
                            FullName = eachStudentHistory.StudentDetails.User.FullName,
                            ArmName = eachStudentHistory.StudentDetails.ClassArm.ArmName,
                            ClassLabel = eachStudentHistory.Classs.ClassLabel,
                            CurrentClass = eachStudentHistory.Classs.ClassName,
                            AdmisionNo = eachStudentHistory.StudentDetails.IdNo,
                            StudentLevelPaymentId = eachStudentHistory.Id,
                            ClassCategoryStatus = actualClassCategory,
                            StudentStatus = eachStudentHistory.StudentDetails.Status,
                            CurrentTermPaymentStatus = currentFeePaymentStatus,
                            OtherTermsPaymentStatus = otherPaymentStatus
                        });
                    }
                }
            }
            return studentFieldExtractionList;
        }

        private string GeneratePin(int pinlength)
        {
            var random = new Random();

            var myStringArray = "123456789ABCDEFGHJKLMNPQRSTUVWXYabdeghnrt".ToCharArray();
            //var pin = GenerateVoucherPin(myArray, 10);
            return Enumerable
                .Range(1, pinlength) // for(i.. ) 
                .Select(k => myStringArray[random.Next(0, myStringArray.Length - 1)]) // generate a new random char 
                .Aggregate("", (e, c) => e + c); // join into a string        
        }

        public string PaymentIdGen(int codeLenth)
        {
            string code = null;
            var strTest = false;
            while (strTest == false)
            {
                code = GeneratePin(codeLenth);
                var code1 = code;
                var isCodeAvailable =
                    ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPayment, int>>()
                        .Find(t => t.PaymentId == code1);
                if (isCodeAvailable != null)
                {
                    code = GeneratePin(codeLenth);
                }
                else
                {
                    strTest = true;
                }
            }
            return code;
        }

        public List<StudentFieldExtractionForPaymentReceiptDto> PayForSelectedReturningStudents(
            List<StudentIdsAndClassCategoryBto> studentIds)
        {
            var studentTyTermlyFeePaymentRepository =
                ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPayment, int>>();
            var employeeModel = CurrentTeacherModel();
            var schoolId = employeeModel.SchoolId;
            var signature = employeeModel.SignatureImageString;
            var yearlyAndTermlyStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var setSchoolFeesRepository = ServiceLocator.Get<IGenericRepository<SetSchoolFeesTr, int>>();
            var studentHistoryRepository =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
            var yearlyAndTermlyAssessmentStatus =
                yearlyAndTermlyStatusRepository.Find(
                    s =>
                        s.Session.SchoolInfoId == schoolId && s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                    b => b.Session);
            var yearlyAndTermlyAssessmentStatusId = yearlyAndTermlyAssessmentStatus.Id;
            var setSchoolFeesModel =
                setSchoolFeesRepository.Find(
                    s => s.SchoolId == schoolId && s.YearlyAndTermlyStatusId == yearlyAndTermlyAssessmentStatusId);
            if (setSchoolFeesModel == null)
            {
                return null;
            }
            var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var schoolModel = schoolInfoRepository.Find(s => s.SchoolRegistrationOfficerId == schoolId);
            var returnList = new List<StudentFieldExtractionForPaymentReceiptDto>();
            foreach (var eachId in studentIds)
            {
                var currentStudent = studentHistoryRepository.Find(f => f.Id == eachId.Id, d => d.Classs,
                    u => u.StudentDetails.User);
                var amount = GetFeesForReturningStudent(setSchoolFeesModel, currentStudent.StudentDetails.ClassCategory);
                //var studentId = eachId.Id;
                var hisTotalPay =
                    studentTyTermlyFeePaymentRepository.Where(
                        x => x.StudentAcademicProgressHistoryId == eachId.Id).Sum(x => x.Amount);
                if (hisTotalPay == 0)
                {
                    var paymentId = "";
                    if (eachId.PaymentId == "Auto")
                    {
                        paymentId = "CA" + PaymentIdGen(8);
                    }
                    else
                    {
                        if (eachId.PaymentType == PaymentType.Cash)
                        {
                            paymentId = "CA" + eachId.PaymentId;
                        }
                        else if (eachId.PaymentType == PaymentType.Teller)
                        {
                            paymentId = "TE" + eachId.PaymentId;
                        }
                        else
                        {
                            paymentId = "TR" + eachId.PaymentId;
                        }
                    }
                    eachId.PaymentDate = eachId.PaymentDate ?? DateTime.Now;
                    var eachPayment = new StudentTyTermlyFixedPayment
                    {
                        StudentAcademicProgressHistoryId = eachId.Id,
                        Amount = amount,
                        PaymentId = paymentId,
                        PaymentType = eachId.PaymentType,
                        PaymentDate = (DateTime)eachId.PaymentDate
                    };
                    studentTyTermlyFeePaymentRepository.Insert(eachPayment);
                    var amountToNaira = amount.ToString("C").Replace('£', '₦');
                    returnList.Add(new StudentFieldExtractionForPaymentReceiptDto
                    {
                        AdmisionNo = currentStudent.StudentDetails.IdNo,
                        ClassLabel = currentStudent.Classs.ClassLabel,
                        FullName = currentStudent.StudentDetails.User.FullName,
                        TermAndSession =
                            yearlyAndTermlyAssessmentStatus.TermStatus + ", " +
                            yearlyAndTermlyAssessmentStatus.Session.Session,
                        Fees = amountToNaira,
                        SchoolLogo = schoolModel.SchoolLogoString,
                        SchoolAddress =
                            "<h4 style='margin:0; text-align:center; padding:0px'>" + schoolModel.SchoolName +
                            "</h4><strong>Address: </strong> " + schoolModel.SchoolAddress +
                            " <br /><div class='text-center'><strong>Tel: </strong>" + schoolModel.Telephone + "</div>",
                        AccountSignature = signature,
                        PaymentId = paymentId,
                        PaymentDate = eachId.PaymentDate.Value.ToLongDateString()
                    });
                }
            }
            return returnList;
        }

        public SetSchoolFeesDto GetClassLevelSchoolFees()
        {
            var schoolId = CurrentTeacherModel().SchoolId;
            var yearlyAndTermlyStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var setSchoolFeesRepository = ServiceLocator.Get<IGenericRepository<SetSchoolFeesTr, int>>();
            var yearlyAndTermlyAssessmentStatusId =
                yearlyAndTermlyStatusRepository.Find(
                    s =>
                        s.Session.SchoolInfoId == schoolId && s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current)
                    ?.Id;
            if (yearlyAndTermlyAssessmentStatusId != null)
            {
                var setSchoolFeesModel =
                    setSchoolFeesRepository.Find(
                        s => s.SchoolId == schoolId && s.YearlyAndTermlyStatusId == yearlyAndTermlyAssessmentStatusId);
                if (setSchoolFeesModel == null)
                {
                    var previousFees = setSchoolFeesRepository.Where(x => x.SchoolId == schoolId)?.ToList();
                    if (previousFees != null && previousFees.Any())
                    {
                        var lastFeesModel =
                            previousFees.OrderByDescending(x => x.YearlyAndTermlyStatusId).ToList().First();
                        lastFeesModel.YearlyAndTermlyStatusId = (int)yearlyAndTermlyAssessmentStatusId;
                        setSchoolFeesRepository.Insert(lastFeesModel);
                        UnitOfWork.SaveChanges();
                        var currentFees =
                            setSchoolFeesRepository.Find(
                                x => x.YearlyAndTermlyStatusId == yearlyAndTermlyAssessmentStatusId);
                        return Mapper.Map<SetSchoolFeesDto>(currentFees);
                    }
                    return new SetSchoolFeesDto();
                }
                return Mapper.Map<SetSchoolFeesDto>(setSchoolFeesModel);
            }
            return null;
        }

        public byte AddOrUpdateFeePrices(SetSchoolFeesDto model)
        {
            var setSchoolFeesRepository = ServiceLocator.Get<IGenericRepository<SetSchoolFeesTr, int>>();
            var schoolId = CurrentTeacherModel().SchoolId;

            ////The code below is required in order to avoid wrong update of fees of the previous term when attempting to make changes to fees for the current term.
            ////Logic, Make sure this is done immediately after you round up the previous term when all the ArmTermlyAssessmentStatus is un-completed
            //var teacherClassArmAssignment = ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
            //var numberOfFormMasters = teacherClassArmAssignment.Where(d => d.Employee.SchoolId == schoolId).Count();
            //var noOfFormMasterThatHaveNotRoundUpTheTerm =
            //    teacherClassArmAssignment.Where(
            //        s =>
            //            s.Employee.SchoolId == schoolId &&
            //            s.ArmTermlyAssessmentStatus == ArmTermlyAssessmentStatus.Uncompleted).Count();
            //if (numberOfFormMasters == 0)
            //{
            //    return 1;
            //    //Failed! Other registration need to be done before here especially up to a point where student Assessment Preparation is done 
            //}
            //if (numberOfFormMasters != noOfFormMasterThatHaveNotRoundUpTheTerm)
            //{
            //    return 2;
            //    // This fees update has to be done durring the begining of the term. Please contact Admin officer.
            //}
            ////End

            //Check if the fees has been set for this term
            var currentFeesModel =
                setSchoolFeesRepository.Find(
                    s =>
                        s.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        s.SchoolId == schoolId);
            if (currentFeesModel == null)
            {
                var yearlyAndTermlyStatusRepository =
                    ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
                var yearlyAndTermlyStatus =
                    yearlyAndTermlyStatusRepository.Find(
                        s =>
                            s.Session.SchoolInfoId == schoolId &&
                            s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current);
                if (yearlyAndTermlyStatus == null)
                {
                    return 3;
                    //Failed! Other setting need to be done before here especially up to a point where student Assessment Preparation is done
                }
                model.SchoolId = schoolId;
                model.YearlyAndTermlyStatusId = yearlyAndTermlyStatus.Id;
                setSchoolFeesRepository.Insert(Mapper.Map<SetSchoolFeesTr>(model));
            }
            else
            {
                setSchoolFeesRepository.Update(Mapper.Map<SetSchoolFeesTr>(model));
            }
            return 4;
        }

        public IList<StudentFieldExtraction4AccountingDto> GetFreshiersList()
        {
            var setSchoolFeesPaymentRepository = ServiceLocator.Get<IGenericRepository<SetSchoolFeesTr, int>>();
            var schoolId = CurrentTeacherModel().SchoolId;

            var feesPricesList =
                setSchoolFeesPaymentRepository.Where(
                    d =>
                        d.SchoolId == schoolId, c => c.YearlyAndTermlyStatus).ToList();


            if (!feesPricesList.Any()) //else, 
            {
                return null; //School Fees needs to be set first
            }
            var currentFeeModel =
                feesPricesList.Find(
                    d =>
                        d.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current);
            if (currentFeeModel == null) //else, 
            {
                var lastFeesModel =
                    feesPricesList.OrderByDescending(x => x.YearlyAndTermlyStatusId).ToList().First();

                var yearlyAndTermlyStatusRepository =
                    ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
                //var setSchoolFeesRepository = ServiceLocator.Get<IGenericRepository<SetSchoolFeesTr, int>>();
                var yearlyAndTermlyAssessmentStatusId =
                    yearlyAndTermlyStatusRepository.Find(
                        s =>
                            s.Session.SchoolInfoId == schoolId &&
                            s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).Id;

                lastFeesModel.YearlyAndTermlyStatusId = yearlyAndTermlyAssessmentStatusId;
                setSchoolFeesPaymentRepository.Insert(lastFeesModel);
                UnitOfWork.SaveChanges();
                currentFeeModel =
                    setSchoolFeesPaymentRepository.Find(
                        x => x.YearlyAndTermlyStatusId == yearlyAndTermlyAssessmentStatusId);
            }
            var studentFieldExtractionList = new List<StudentFieldExtraction4AccountingDto>();
            var studentRepository = ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
            var currentTermId = currentFeeModel.YearlyAndTermlyStatusId;
            var newStudent = studentRepository.Where(
                x =>
                    x.StudentDetails.SchoolInfoId == schoolId && x.YearlyAndTermlyStatusId == currentTermId &&
                    x.StudentDetails.Status == Common.Shared.StudentStatus.Fresher,
                s => s.StudentDetails.User, a => a.StudentDetails.ClassArm.Classs,
                a => a.Classs)?.ToList();

            var studentTermlyFeePaymentRepository =
                ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPayment, int>>();
            if (newStudent != null && newStudent.Any())
            {
                foreach (var eachStudent in newStudent)
                {
                    var currentStudentPaymentList =
                        studentTermlyFeePaymentRepository.Where(
                            x =>
                                x.StudentAcademicProgressHistory.StudentDetails.Id == eachStudent.StudentId);
                    ClassCategoryStatus classCategory;
                    if (
                        !(eachStudent.Classs.ClassName == CurrentClass.Ss1 ||
                          eachStudent.Classs.ClassName == CurrentClass.Ss2 ||
                          eachStudent.Classs.ClassName == CurrentClass.Ss3))
                    {
                        try
                        {
                            classCategory = GetClassCategoryStatus(eachStudent.StudentDetails.ClassArm.Classs.ClassName);
                        }
                        catch (Exception)
                        {
                            int rap = 1;
                            throw;
                        }
                    }
                    else
                    {
                        classCategory = eachStudent.StudentDetails.ClassCategory;
                    }

                    var hisActualFees = GetFeesForFreshiers(currentFeeModel, classCategory);
                    var hisTotalTermPaymenObjects =
                        currentStudentPaymentList.Sum(x => x.Amount);
                    if (Math.Abs(hisTotalTermPaymenObjects) < 0.9)
                    {
                        studentFieldExtractionList.Add(new StudentFieldExtraction4AccountingDto
                        {
                            FullName = eachStudent.StudentDetails.User.FullName,
                            ArmName = eachStudent.StudentDetails.ClassArm.ArmName,
                            ClassLabel = eachStudent.StudentDetails.ClassArm.Classs.ClassLabel,
                            CurrentClass = eachStudent.StudentDetails.ClassArm.Classs.ClassName,
                            AdmisionNo = eachStudent.StudentDetails.IdNo,
                            StudentLevelPaymentId = eachStudent.Id,
                            ClassCategoryStatus = classCategory,
                            StudentStatus = eachStudent.StudentDetails.Status,
                            CurrentTermPaymentStatus = FeesPaymentStatus.None,
                            OtherTermsPaymentStatus = FeesPaymentStatus.Completed,

                        });
                    }
                    else if (hisTotalTermPaymenObjects > 0 &&
                             hisTotalTermPaymenObjects < hisActualFees)
                    {
                        var feePaymentDiscount =
                            ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPaymentDiscount, short>>()
                                .Find(z => z.StudentAcademicProgressId == eachStudent.Id);
                        if (feePaymentDiscount == null)
                        {
                            studentFieldExtractionList.Add(new StudentFieldExtraction4AccountingDto
                            {
                                FullName = eachStudent.StudentDetails.User.FullName,
                                ArmName = eachStudent.StudentDetails.ClassArm.ArmName,
                                ClassLabel = eachStudent.StudentDetails.ClassArm.Classs.ClassLabel,
                                CurrentClass = eachStudent.StudentDetails.ClassArm.Classs.ClassName,
                                AdmisionNo = eachStudent.StudentDetails.IdNo,
                                StudentLevelPaymentId = eachStudent.Id,
                                ClassCategoryStatus = classCategory,
                                StudentStatus = eachStudent.StudentDetails.Status,
                                CurrentTermPaymentStatus = FeesPaymentStatus.PartPayment,
                                OtherTermsPaymentStatus = FeesPaymentStatus.Completed,

                            });
                        }
                    }
                }
            }
            else
            {
                return studentFieldExtractionList;
                //No new student has been registered this term
            }
            return studentFieldExtractionList;
        }

        public List<StudentFieldExtractionForPaymentReceiptDto> PayForSelectedFreshiersRegistration(
            List<StudentIdsAndClassCategoryBto> studentIds)
        {
            var studentTyTermlyFeePaymentRepository =
                ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPayment, int>>();
            var employeeModel = CurrentTeacherModel();
            var schoolId = employeeModel.SchoolId;
            var signature = employeeModel.SignatureImageString;
            var yearlyAndTermlyStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var setSchoolFeesRepository = ServiceLocator.Get<IGenericRepository<SetSchoolFeesTr, int>>();

            var yearlyAndTermlyAssessmentStatus =
                yearlyAndTermlyStatusRepository.Find(
                    s =>
                        s.Session.SchoolInfoId == schoolId && s.TermlyAccessmentStatus == TermlyAccessmentStatus.Current,
                    g => g.Session);
            var setSchoolFeesModel =
                setSchoolFeesRepository.Find(
                    s => s.SchoolId == schoolId && s.YearlyAndTermlyStatusId == yearlyAndTermlyAssessmentStatus.Id);
            if (setSchoolFeesModel == null)
            {
                return null;
            }
            var returnList = new List<StudentFieldExtractionForPaymentReceiptDto>();
            var studentHistoryRepository =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
            var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var schoolModel = schoolInfoRepository.Find(s => s.SchoolRegistrationOfficerId == schoolId);
            foreach (var eachId in studentIds)
            {
                var currentStudent = studentHistoryRepository.Find(f => f.Id == eachId.Id, d => d.Classs,
                    u => u.StudentDetails.User, v => v.StudentDetails.ClassArm.Classs);
                double amount = 0;
                var classs = currentStudent.StudentDetails.ClassArm.Classs.ClassName;
                if (!(classs == CurrentClass.Ss1 || classs == CurrentClass.Ss2 || classs == CurrentClass.Ss3))
                {
                    var className3 = GetClassCategoryStatus(classs);
                    {
                        amount = GetFeesForFreshiers(setSchoolFeesModel, className3); // To avoid Mistake
                    }
                }
                else
                {
                    amount = GetFeesForFreshiers(setSchoolFeesModel, currentStudent.StudentDetails.ClassCategory);
                }
                var hisTotalPay =
                    studentTyTermlyFeePaymentRepository.Where(
                        x => x.StudentAcademicProgressHistoryId == eachId.Id).Sum(x => x.Amount);
                if (Math.Abs(hisTotalPay) < 0.9)
                {
                    var paymentId = "";
                    if (eachId.PaymentId == "Auto")
                    {
                        paymentId = "CA" + PaymentIdGen(8);
                    }
                    else
                    {
                        if (eachId.PaymentType == PaymentType.Cash)
                        {
                            paymentId = "CA" + eachId.PaymentId;
                        }
                        else if (eachId.PaymentType == PaymentType.Teller)
                        {
                            paymentId = "TE" + eachId.PaymentId;
                        }
                        else
                        {
                            paymentId = "TR" + eachId.PaymentId;
                        }
                    }

                    eachId.PaymentDate = eachId.PaymentDate ?? DateTime.Now;
                    var eachPayment = new StudentTyTermlyFixedPayment
                    {
                        StudentAcademicProgressHistoryId = eachId.Id,
                        Amount = amount,
                        PaymentId = paymentId,
                        PaymentType = eachId.PaymentType,
                        PaymentDate = (DateTime)eachId.PaymentDate
                    };

                    var amountToNaira = amount.ToString("C").Replace('£', '₦');
                    returnList.Add(new StudentFieldExtractionForPaymentReceiptDto
                    {
                        AdmisionNo = currentStudent.StudentDetails.IdNo,
                        ClassLabel = currentStudent.Classs.ClassLabel,
                        FullName = currentStudent.StudentDetails.User.FullName,
                        TermAndSession =
                            yearlyAndTermlyAssessmentStatus.TermStatus + ", " +
                            yearlyAndTermlyAssessmentStatus.Session.Session,
                        Fees = amountToNaira,
                        SchoolLogo = schoolModel.SchoolLogoString,
                        SchoolAddress =
                            "<h4 style='margin:0; text-align:center; padding:0px'>" + schoolModel.SchoolName +
                            "</h4><strong>Address: </strong> " + schoolModel.SchoolAddress +
                            " <br /><div class='text-center'><strong>Tel: </strong>" + schoolModel.Telephone + "</div>",
                        AccountSignature = signature,
                        PaymentId = paymentId,
                        PaymentDate = eachId.PaymentDate.Value.ToLongDateString()
                    });
                    studentTyTermlyFeePaymentRepository.Insert(eachPayment);
                }
            }
            return returnList;
        }

        public List<StudentTermlyFees2Nd> GetSelectedReturningStudentPaymentHistoryById(int studentLevelPaymentId)
        {
            var schoolId = CurrentTeacherModel().SchoolId;
            var schoolFeesRepository = ServiceLocator.Get<IGenericRepository<SetSchoolFeesTr, int>>();
            var feesCollection =
                schoolFeesRepository.Where(s => s.SchoolId == schoolId, v => v.YearlyAndTermlyStatus.Session)
                    .ToList();
            var studentAcademicHistoryRepo =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();

            var studentModel =
                studentAcademicHistoryRepo.Find(z => z.Id == studentLevelPaymentId, d => d.StudentTermlyFeePayment,
                    c => c.StudentDetails)
                    .StudentDetails;

            var termIdOfStartUsingAccountingFeature =
                ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>()
                    .Find(z => z.SchoolRegistrationOfficerId == schoolId)
                    .AccountingStartTermId;


            var terminalFeesDetails = new List<StudentTermlyFees2Nd>();
            var listOfPaymentsSoFar = studentAcademicHistoryRepo
                .Where(c =>
                    c.YearlyAndTermlyStatusId >=
                    termIdOfStartUsingAccountingFeature && c.StudentId == studentModel.Id &&
                    c.YearlyAndTermlyStatusId >= studentModel.AdmisnTermId, x => x.Classs,
                    v => v.StudentTermlyFeePayment)
                .ToList();

            var initialFeePaymentObject =
                listOfPaymentsSoFar.OrderBy(d => d.YearlyAndTermlyStatusId).First();
            var initialTermId = initialFeePaymentObject.YearlyAndTermlyStatusId;
            var actualFeeModel = feesCollection.Find(x => x.YearlyAndTermlyStatusId == initialTermId);

            double initialFeeActualPayment = 0;
            var studentInitialRegStatus = listOfPaymentsSoFar.First().StudentDetails.StudentInitialRegStatus;
            var classss = listOfPaymentsSoFar.First().Classs.ClassName;

            if (studentInitialRegStatus == StudentInitialRegistrationStatus.Fresher)
            {
                if (!(classss == CurrentClass.Ss1 || classss == CurrentClass.Ss2 || classss == CurrentClass.Ss3))
                {
                    var className3 =
                        GetClassCategoryStatus(listOfPaymentsSoFar.First().Classs.ClassName);
                    {
                        initialFeeActualPayment = GetFeesForFreshiers(actualFeeModel, className3);
                        // To avoid Mistake
                    }
                }
                else
                {
                    initialFeeActualPayment = GetFeesForFreshiers(actualFeeModel,
                        listOfPaymentsSoFar.First().StudentDetails.ClassCategory);
                }
            }
            else
            {
                if (!(classss == CurrentClass.Ss1 || classss == CurrentClass.Ss2 || classss == CurrentClass.Ss3))
                {
                    var className3 =
                        GetClassCategoryStatus(listOfPaymentsSoFar.First().Classs.ClassName);
                    {
                        initialFeeActualPayment = GetFeesForReturningStudent(actualFeeModel, className3);
                        // To avoid Mistake
                    }
                }
                else
                {
                    initialFeeActualPayment = GetFeesForReturningStudent(actualFeeModel,
                        listOfPaymentsSoFar.First().StudentDetails.ClassCategory);
                }
            }

            var totalPayment = listOfPaymentsSoFar.First().StudentTermlyFeePayment.Sum(x => x.Amount);
            if (totalPayment < initialFeeActualPayment)
            {
                var studentHistoryId = listOfPaymentsSoFar.First().Id;
                var feePaymentDiscount =
                    ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPaymentDiscount, short>>()
                        .Find(z => z.StudentAcademicProgressId == studentHistoryId);
                if (feePaymentDiscount == null)
                {
                    terminalFeesDetails.Add(new StudentTermlyFees2Nd
                    {
                        //StudentId = studentModel.Id,
                        //IdNo = studentModel.IdNo,
                        //FullName = studentModel.User.FullName,
                        StudentAcademicLevelId = studentHistoryId,
                        TermStatus = listOfPaymentsSoFar.First().YearlyAndTermlyStatus.TermStatus,
                        //SessionId = (byte)eachTerm.First().StudentAcademicProgressHistory.YearlyAndTermlyStatus.SessionId,
                        Session = listOfPaymentsSoFar.First().YearlyAndTermlyStatus.Session.Session,
                        //ClassLabel = eachTerm.First().StudentAcademicProgressHistory.Classs.ClassLabel,
                        ActualFees = initialFeeActualPayment.ToString("C").Replace('£', '₦'),
                        AmountPaid = totalPayment.ToString("C").Replace('£', '₦'),
                        Outstanding = (initialFeeActualPayment - totalPayment).ToString("C").Replace('£', '₦')
                    });
                }
            }
            listOfPaymentsSoFar.Remove(listOfPaymentsSoFar.First());

            if (listOfPaymentsSoFar.Any())
            {
                foreach (var each in listOfPaymentsSoFar)
                {
                    var returningStudentFee = 0.0;
                    actualFeeModel = feesCollection.Find(x => x.YearlyAndTermlyStatusId == each.YearlyAndTermlyStatusId);
                    if (!(classss == CurrentClass.Ss1 || classss == CurrentClass.Ss2 || classss == CurrentClass.Ss3))
                    {
                        var classCategory = GetClassCategoryStatus(listOfPaymentsSoFar.First().Classs.ClassName);
                        {
                            returningStudentFee = GetFeesForReturningStudent(actualFeeModel, classCategory);
                            // To avoid Mistake
                        }
                    }
                    else
                    {
                        returningStudentFee = GetFeesForReturningStudent(actualFeeModel,
                            each.StudentDetails.ClassCategory);
                    }

                    if (each.StudentTermlyFeePayment.Sum(x => x.Amount) < returningStudentFee)
                    {
                        var studentHistoryId = each.Id;
                        var feePaymentDiscount =
                            ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPaymentDiscount, short>>()
                                .Find(z => z.StudentAcademicProgressId == studentHistoryId);
                        if (feePaymentDiscount == null)
                        {
                            terminalFeesDetails.Add(new StudentTermlyFees2Nd
                            {
                                StudentAcademicLevelId = each.Id,
                                TermStatus = each.YearlyAndTermlyStatus.TermStatus,
                                //SessionId = (byte)eachTerm.First().StudentAcademicProgressHistory.YearlyAndTermlyStatus.SessionId,
                                Session = each.YearlyAndTermlyStatus.Session.Session,
                                //ClassLabel = eachTerm.First().StudentAcademicProgressHistory.Classs.ClassLabel,
                                ActualFees = returningStudentFee.ToString("C").Replace('£', '₦'),
                                AmountPaid =
                                    each.StudentTermlyFeePayment.Sum(x => x.Amount).ToString("C").Replace('£', '₦'),
                                Outstanding =
                                    (returningStudentFee - each.StudentTermlyFeePayment.Sum(x => x.Amount)).ToString("C")
                                        .Replace('£', '₦')
                            });
                        }
                    }
                }

            }
            return terminalFeesDetails;
        }


        public SingleStudentTermlyFeesReturnData SingleStudentCurrentTermsFeePartPayment(int studentLevelPaymentId,
            double hisFee, double actualFees, PaymentType paymentType, string paymentId, DateTime? paymentDate,
            double discount, string reasonForDiscount)
        {
            var studentTyTermlyFeePaymentRepository =
                ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPayment, int>>();

            var teacherModel = CurrentTeacherModel();

            var schoolModel = teacherModel.SchoolInfo;
            if (hisFee <= actualFees)
            {

                if (paymentId == "Auto")
                {
                    paymentId = "CA" + PaymentIdGen(8);
                }
                else
                {
                    if (paymentType == PaymentType.Cash)
                    {
                        paymentId = "CA" + paymentId;
                    }
                    else if (paymentType == PaymentType.Teller)
                    {
                        paymentId = "TE" + paymentId;
                    }
                    else
                    {
                        paymentId = "TR" + paymentId;
                    }
                }

                var eachPayment = new StudentTyTermlyFixedPayment
                {
                    StudentAcademicProgressHistoryId = studentLevelPaymentId,
                    Amount = hisFee,
                    PaymentId = paymentId,
                    PaymentType = paymentType,
                    PaymentDate = DateTime.Now
                };
                studentTyTermlyFeePaymentRepository.Insert(eachPayment);

                if (discount > 0)
                {
                    var feePaymentDiscountRepository =
                        ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPaymentDiscount, short>>();
                    var modelObject = new StudentTyTermlyFixedPaymentDiscount
                    {
                        StudentAcademicProgressId = studentLevelPaymentId,
                        DiscountAmount = discount,
                        ReasonsForDiscount = reasonForDiscount
                    };
                    feePaymentDiscountRepository.Insert(modelObject);
                }
                //student.Status = Common.Shared.StudentStatus.Returning;
                return new SingleStudentTermlyFeesReturnData
                {
                    SchoolName = schoolModel.SchoolName,
                    SchoolLogo = schoolModel.SchoolLogoString,
                    SchoolAddress = schoolModel.SchoolAddress,
                    SchoolTel = schoolModel.Telephone,
                    SchoolId = schoolModel.SchoolRegistrationOfficerId,
                    AccountSignature = teacherModel.SignatureImageString,
                    PaymentId = paymentId
                };
            }
            return new SingleStudentTermlyFeesReturnData();
        }

        public List<StudentTermlyFees2Nd> GetSelectedStudentFreshPaymentHistoryById(int studentLevelPaymentId)
        {
            var setSchoolFeesRepository = ServiceLocator.Get<IGenericRepository<SetSchoolFeesTr, int>>();
            var studentHistoryRepository =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
            var studentHistoryObject = studentHistoryRepository.Find(z => z.Id == studentLevelPaymentId, x => x.Classs,
                d => d.StudentDetails, c => c.StudentDetails.ClassArm.Classs);
            var schoolId = studentHistoryObject.Classs.SchoolInfoId;

            var studentTyTermlyFeePaymentRepository =
                ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPayment, int>>();

            var paymentList =
                studentTyTermlyFeePaymentRepository.Where(
                    d =>
                        d.StudentAcademicProgressHistory.StudentId == studentHistoryObject.StudentId &&
                        d.StudentAcademicProgressHistory.YearlyAndTermlyStatus.TermlyAccessmentStatus ==
                        TermlyAccessmentStatus.Current,
                    s => s.StudentAcademicProgressHistory.StudentDetails.User)?.ToList();
            var termFeeModel =
                setSchoolFeesRepository.Find(s => s.SchoolId == schoolId &&
                                                  s.YearlyAndTermlyStatus.TermlyAccessmentStatus ==
                                                  TermlyAccessmentStatus.Current, x => x.YearlyAndTermlyStatus.Session);
            var className = studentHistoryObject.StudentDetails.ClassArm.Classs.ClassName;
            ClassCategoryStatus classCategory;
            if (
                !(className == CurrentClass.Ss1 ||
                  className == CurrentClass.Ss2 ||
                  className == CurrentClass.Ss3))
            {
                classCategory =
                    GetClassCategoryStatus(className);
            }
            else
            {
                classCategory = studentHistoryObject.StudentDetails.ClassCategory;
            }
            var actualFee = GetFeesForFreshiers(termFeeModel, classCategory);

            if (paymentList != null && paymentList.Any())
            {

                if (paymentList.Sum(x => x.Amount) < actualFee)
                {
                    var feePaymentDiscount =
                        ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPaymentDiscount, short>>()
                            .Find(z => z.StudentAcademicProgressId == studentHistoryObject.Id);
                    if (feePaymentDiscount == null)
                    {
                        return new List<StudentTermlyFees2Nd>
                        {
                            new StudentTermlyFees2Nd
                            {
                                //StudentId = studentModel.Id,
                                //IdNo = studentModel.IdNo,
                                //FullName = studentModel.User.FullName,
                                StudentAcademicLevelId = studentLevelPaymentId,
                                TermStatus = termFeeModel.YearlyAndTermlyStatus.TermStatus,
                                //SessionId = (byte)eachTerm.First().YearlyAndTermlyStatus.SessionId,
                                Session = termFeeModel.YearlyAndTermlyStatus.Session.Session,
                                //ClassLabel = eachTerm.First().StudentAcademicProgressHistory.Classs.ClassLabel,
                                ActualFees = actualFee.ToString("C").Replace('£', '₦'),
                                AmountPaid = paymentList.Sum(v => v.Amount).ToString("C").Replace('£', '₦'),
                                Outstanding =
                                    (actualFee - paymentList.Sum(v => v.Amount)).ToString("C")
                                        .Replace('£', '₦')
                            }
                        };
                    }
                }
            }
            else
            {
                return new List<StudentTermlyFees2Nd>
                {
                    new StudentTermlyFees2Nd
                    {
                        //StudentId = studentModel.Id,
                        //IdNo = studentModel.IdNo,
                        //FullName = studentModel.User.FullName,
                        StudentAcademicLevelId = studentLevelPaymentId, //(short) termFeeModel.YearlyAndTermlyStatusId,
                        TermStatus = termFeeModel.YearlyAndTermlyStatus.TermStatus,
                        //SessionId = (byte)eachTerm.First().YearlyAndTermlyStatus.SessionId,
                        Session = termFeeModel.YearlyAndTermlyStatus.Session.Session,
                        //ClassLabel = eachTerm.First().StudentAcademicProgressHistory.Classs.ClassLabel,
                        ActualFees = actualFee.ToString("C").Replace('£', '₦'),
                        AmountPaid = "0",
                        Outstanding = actualFee.ToString("C").Replace('£', '₦')
                    }
                };
            }

            return null;
        }

        public IList<SelectListItem> GetTermsDropdownForSelectedSession(int selectedSessionId)
        {
            var yearlyAndTermlyStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var terms = yearlyAndTermlyStatusRepository.Where(h => h.SessionId == selectedSessionId)?.ToList();
            if (terms != null && terms.Any())
            {
                return
                    terms.OrderBy(v => v.TermStatus)
                        .Select(x => new SelectListItem { Text = x.TermStatus.ToString(), Value = x.Id.ToString() })
                        .ToList();
            }
            return new List<SelectListItem>();
        }

        private ClassCategoryStatus GetClassCategoryStatus(CurrentClass className)
        {
            ClassCategoryStatus status = ClassCategoryStatus.PlayClass;
            if (className == CurrentClass.PreNursery)
            {
                status = ClassCategoryStatus.PreNursery;
            }
            else if (className == CurrentClass.Nursery1 || className == CurrentClass.Nursery2)
            {
                status = ClassCategoryStatus.Nursery;
            }
            else if (className == CurrentClass.Primary1 || className == CurrentClass.Primary2
                     || className == CurrentClass.Primary3 || className == CurrentClass.Primary4
                     || className == CurrentClass.Primary5 || className == CurrentClass.Primary6)
            {
                status = ClassCategoryStatus.Primary;
            }
            else if (className == CurrentClass.Jss1 || className == CurrentClass.Jss2
                     || className == CurrentClass.Jss3)
            {
                status = ClassCategoryStatus.Jss;
            }
            return status;
        }

        public TermlyPaymentReportDto GetTermlyReportByTermId(int selectedTermId)
        {

            var playClassFreshClassCategory = ClassCategoryStatus.None;
            var preNurseryFreshClassCategory = ClassCategoryStatus.None;
            var nurseryFreshClassCategory = ClassCategoryStatus.None;
            var primaryFreshClassCategory = ClassCategoryStatus.None;
            var jssFreshClassCategory = ClassCategoryStatus.None;
            var scienceFreshClassCategory = ClassCategoryStatus.None;
            var artFreshClassCategory = ClassCategoryStatus.None;
            var commercialFreshClassCategory = ClassCategoryStatus.None;

            var playClassRetrnClassCategory = ClassCategoryStatus.None;
            var preNurseryRetrnClassCategory = ClassCategoryStatus.None;
            var nurseryRetrnClassCategory = ClassCategoryStatus.None;
            var primaryRetrnClassCategory = ClassCategoryStatus.None;
            var jssRetrnClassCategory = ClassCategoryStatus.None;
            var scienceRetrnClassCategory = ClassCategoryStatus.None;
            var artRetrnClassCategory = ClassCategoryStatus.None;
            var commercialRetrnClassCategory = ClassCategoryStatus.None;

            var schoolId = CurrentTeacherModel().SchoolId;
            var setSchoolFeesRepository = ServiceLocator.Get<IGenericRepository<SetSchoolFeesTr, int>>();
            var currentFeesObject =
                setSchoolFeesRepository.Find(
                    d =>
                        d.SchoolId == schoolId && d.YearlyAndTermlyStatusId == selectedTermId,
                    x => x.YearlyAndTermlyStatus.Session);
            if (currentFeesObject == null)
            {
                return new TermlyPaymentReportDto { TermlyReportOfReturning = new List<TermlyReportOfReturningDto>() };
                //Price needs to be first set
            }

            var freshPlayClass = currentFeesObject.FreshPlayClass;
            var freshPreNursery = currentFeesObject.FreshPreNursery;
            var freshNursery = currentFeesObject.FreshNursery;
            var freshPrimary = currentFeesObject.FreshPrimary;
            var freshJss = currentFeesObject.FreshJss;
            var freshScience = currentFeesObject.FreshScience;
            var freshArt = currentFeesObject.FreshArt;
            var freshCommercial = currentFeesObject.FreshCommercial;

            var retnPlayClass = currentFeesObject.RetnPlayClass;
            var retnPreNursery = currentFeesObject.RetnPreNursery;
            var retnNursery = currentFeesObject.RetnNursery;
            var retnPrimary = currentFeesObject.RetnPrimary;
            var retnJss = currentFeesObject.RetnJss;
            var retnScience = currentFeesObject.RetnScience;
            var retnArt = currentFeesObject.RetnArt;
            var retnCommercial = currentFeesObject.RetnCommercial;

            var termAndSession = currentFeesObject.YearlyAndTermlyStatus.TermStatus.ToString() + ", " +
                                 currentFeesObject.YearlyAndTermlyStatus.Session.Session;

            var studentFieldExtractionList = new List<TermlyReportOfReturningDto>();
            //var studentTermlyFeePaymentRepository =
            //  ServiceLocator.Get<IGenericRepository<StudentTyTermlyFeePayment, int>>();
            var currentTermListOfStudentsPayment =
                ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPayment, int>>().Where(
                    x =>
                        x.StudentAcademicProgressHistory.YearlyAndTermlyStatusId == selectedTermId &&
                        x.StudentAcademicProgressHistory.StudentDetails.SchoolInfoId == schoolId,
                    d => d.StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs,
                    z => z.StudentAcademicProgressHistory.StudentDetails.ClassArm,
                    x => x.StudentAcademicProgressHistory.StudentDetails.User)?.ToList();
            if (currentTermListOfStudentsPayment == null || !currentTermListOfStudentsPayment.Any())
            {
                return new TermlyPaymentReportDto { TermlyReportOfReturning = new List<TermlyReportOfReturningDto>() };
                //No Payment has been made for this term
            } //To know the actual number of student in the school during the term, 
            var listOfStudentDuringTheSelectedTerm =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>().
                    Where(x => x.YearlyAndTermlyStatusId == selectedTermId, y => y.StudentDetails.User,
                        g => g.StudentDetails.ClassArm.Classs, z => z.Classs, v => v.YearlyAndTermlyStatus.Session)
                    .ToList();

            var freshierNonePaymentStudentCounterForPlayClass = 0;
            var freshierNonePaymentStudentInfoForPlayClass = "";
            double freshierNonePaymentStudentOutstandingPaymentTotalForPlayClass = 0;


            var freshierNonePaymentStudentCounterForPreNursery = 0;
            var freshierNonePaymentStudentInfoForPreNursery = "";
            double freshierNonePaymentStudentOutstandingPaymentTotalForPreNursery = 0;

            var freshierNonePaymentStudentCounterForNursery = 0;
            var freshierNonePaymentStudentInfoForNursery = "";
            double freshierNonePaymentStudentOutstandingPaymentTotalForNursery = 0;

            var freshierNonePaymentStudentCounterForPrimary = 0;
            var freshierNonePaymentStudentInfoForPrimary = "";
            double freshierNonePaymentStudentOutstandingPaymentTotalForPrimary = 0;

            var freshierNonePaymentStudentCounterForJss = 0;
            var freshierNonePaymentStudentInfoForJss = "";
            double freshierNonePaymentStudentOutstandingPaymentTotalForJss = 0;

            var freshierNonePaymentStudentCounterForScience = 0;
            var freshierNonePaymentStudentInfoForScience = "";
            double freshierNonePaymentStudentOutstandingPaymentTotalForScience = 0;

            var freshierNonePaymentStudentCounterForArt = 0;
            var freshierNonePaymentStudentInfoForArt = "";
            double freshierNonePaymentStudentOutstandingPaymentTotalForArt = 0;

            var freshierNonePaymentStudentCounterForCommercial = 0;
            var freshierNonePaymentStudentInfoForCommercial = "";
            double freshierNonePaymentStudentOutstandingPaymentTotalForCommercial = 0;

            var freshiersWithCompletePaymentCounterForPlayClass = 0;
            var freshiersInfoWithCompletePaymentForPlayClass = "";
            double freshiersWithCompletePaymentTotalAmountForPlayClass = 0;

            var freshiersWithCompletePaymentCounterForPreNursery = 0;
            var freshiersInfoWithCompletePaymentForPreNursery = "";
            double freshiersWithCompletePaymentTotalAmountForPreNursery = 0;

            var freshiersWithCompletePaymentCounteForNursery = 0;
            var freshiersInfoWithCompletePaymentForNursery = "";
            double freshiersWithCompletePaymentTotalAmountForNursery = 0;

            var freshiersWithCompletePaymentCounteForPrimary = 0;
            var freshiersInfoWithCompletePaymentForPrimary = "";
            double freshiersWithCompletePaymentTotalAmountForPrimary = 0;

            var freshiersWithCompletePaymentCounteForJss = 0;
            var freshiersInfoWithCompletePaymentForJss = "";
            double freshiersWithCompletePaymentTotalAmountForJss = 0;

            var freshiersWithCompletePaymentCounteForScience = 0;
            var freshiersInfoWithCompletePaymentForScience = "";
            double freshiersWithCompletePaymentTotalAmountForScience = 0;

            var freshiersWithCompletePaymentCounteForArt = 0;
            var freshiersInfoWithCompletePaymentForArt = "";
            double freshiersWithCompletePaymentTotalAmountForArt = 0;

            var freshiersWithCompletePaymentCounteForCommercial = 0;
            var freshiersInfoWithCompletePaymentForCommercial = "";
            double freshiersWithCompletePaymentTotalAmountForCommercial = 0;

            var freshiersWithOutstandingPaymentCounterForPlayClass = 0;
            var freshiersInfoWithOutstandingPaymentForPlayClass = "";
            double freshiersWithOutstandingPaymentTotalAmountForPlayClass = 0;
            double freshiersPartPaymentTotalAmountForPlayClass = 0;

            var freshiersWithOutstandingPaymentCounterForPreNursery = 0;
            var freshiersInfoWithOutstandingPaymentForPreNursery = "";
            double freshiersWithOutstandingPaymentTotalAmountForPreNursery = 0;
            double freshiersPartPaymentTotalAmountForPreNursery = 0;

            var freshiersWithOutstandingPaymentCounterForNursery = 0;
            var freshiersInfoWithOutstandingPaymentForNursery = "";
            double freshiersWithOutstandingPaymentTotalAmountForNursery = 0;
            double freshiersPartPaymentTotalAmountForNursery = 0;

            var freshiersWithOutstandingPaymentCounterForPrimary = 0;
            var freshiersInfoWithOutstandingPaymentForPrimary = "";
            double freshiersWithOutstandingPaymentTotalAmountForPrimary = 0;
            double freshiersPartPaymentTotalAmountForPrimary = 0;

            var freshiersWithOutstandingPaymentCounterForJss = 0;
            var freshiersInfoWithOutstandingPaymentForJss = "";
            double freshiersWithOutstandingPaymentTotalAmountForJss = 0;
            double freshiersPartPaymentTotalAmountForJss = 0;

            var freshiersWithOutstandingPaymentCounterForScience = 0;
            var freshiersInfoWithOutstandingPaymentForScience = "";
            double freshiersWithOutstandingPaymentTotalAmountForScience = 0;
            double freshiersPartPaymentTotalAmountForScience = 0;

            var freshiersWithOutstandingPaymentCounterForArt = 0;
            var freshiersInfoWithOutstandingPaymentForArt = "";
            double freshiersWithOutstandingPaymentTotalAmountForArt = 0;
            double freshiersPartPaymentTotalAmountForArt = 0;

            var freshiersWithOutstandingPaymentCounterForCommercial = 0;
            var freshiersInfoWithOutstandingPaymentForCommercial = "";
            double freshiersWithOutstandingPaymentTotalAmountForCommercial = 0;
            double freshiersPartPaymentTotalAmountForCommercial = 0;


            var returningStudentsWithCompletePaymentCounterForPlayClass = 0;
            var returningStudentsInfoWithCompletePaymentForPlayClass = "";
            double returningStudentsWithCompletePaymentTotalAmountForPlayClass = 0;

            var returningStudentsWithCompletePaymentCounterForPreNursery = 0;
            var returningStudentsInfoWithCompletePaymentForPreNursery = "";
            double returningStudentsWithCompletePaymentTotalAmountForPreNursery = 0;

            var returningStudentsWithCompletePaymentCounteForNursery = 0;
            var returningStudentsInfoWithCompletePaymentForNursery = "";
            double returningStudentsWithCompletePaymentTotalAmountForNursery = 0;

            var returningStudentsWithCompletePaymentCounteForPrimary = 0;
            var returningStudentsInfoWithCompletePaymentForPrimary = "";
            double returningStudentsWithCompletePaymentTotalAmountForPrimary = 0;

            var returningStudentsWithCompletePaymentCounteForJss = 0;
            var returningStudentsInfoWithCompletePaymentForJss = "";
            double returningStudentsWithCompletePaymentTotalAmountForJss = 0;

            var returningStudentsWithCompletePaymentCounteForScience = 0;
            var returningStudentsInfoWithCompletePaymentForScience = "";
            double returningStudentsWithCompletePaymentTotalAmountForScience = 0;

            var returningStudentsWithCompletePaymentCounteForArt = 0;
            var returningStudentsInfoWithCompletePaymentForArt = "";
            double returningStudentsWithCompletePaymentTotalAmountForArt = 0;

            var returningStudentsWithCompletePaymentCounteForCommercial = 0;
            var returningStudentsInfoWithCompletePaymentForCommercial = "";
            double returningStudentsWithCompletePaymentTotalAmountForCommercial = 0;

            var returningStudentsWithOutstandingPaymentCounterForPlayClass = 0;
            var returningStudentsInfoWithOutstandingPaymentForPlayClass = "";
            double returningStudentsWithOutstandingPaymentTotalAmountForPlayClass = 0;
            double returningStudentsPartPaymentTotalAmountForPlayClass = 0;

            var returningStudentsWithOutstandingPaymentCounterForPreNursery = 0;
            var returningStudentsInfoWithOutstandingPaymentForPreNursery = "";
            double returningStudentsWithOutstandingPaymentTotalAmountForPreNursery = 0;
            double returningStudentsPartPaymentTotalAmountForPreNursery = 0;

            var returningStudentsWithOutstandingPaymentCounterForNursery = 0;
            var returningStudentsInfoWithOutstandingPaymentForNursery = "";
            double returningStudentsWithOutstandingPaymentTotalAmountForNursery = 0;
            double returningStudentsPartPaymentTotalAmountForNursery = 0;

            var returningStudentsWithOutstandingPaymentCounterForPrimary = 0;
            var returningStudentsInfoWithOutstandingPaymentForPrimary = "";
            double returningStudentsWithOutstandingPaymentTotalAmountForPrimary = 0;
            double returningStudentssPartPaymentTotalAmountForPrimary = 0;

            var returningStudentsWithOutstandingPaymentCounterForJss = 0;
            var returningStudentsInfoWithOutstandingPaymentForJss = "";
            double returningStudentsWithOutstandingPaymentTotalAmountForJss = 0;
            double returningStudentsPartPaymentTotalAmountForJss = 0;

            var returningStudentsWithOutstandingPaymentCounterForScience = 0;
            var returningStudentsInfoWithOutstandingPaymentForScience = "";
            double returningStudentsWithOutstandingPaymentTotalAmountForScience = 0;
            double returningStudentsPartPaymentTotalAmountForScience = 0;

            var returningStudentsWithOutstandingPaymentCounterForArt = 0;
            var returningStudentsInfoWithOutstandingPaymentForArt = "";
            double returningStudentsWithOutstandingPaymentTotalAmountForArt = 0;
            double returningStudentsPartPaymentTotalAmountForArt = 0;

            var returningStudentsWithOutstandingPaymentCounterForCommercial = 0;
            var returningStudentsInfoWithOutstandingPaymentForCommercial = "";
            double returningStudentsWithOutstandingPaymentTotalAmountForCommercial = 0;
            double returningStudentsPartPaymentTotalAmountForCommercial = 0;

            var returningStudentNonePaymentStudentCounterForPlayClass = 0;
            var returningStudentNonePaymentStudentInfoForPlayClass = "";
            double returningStudentNonePaymentStudentOutstandingPaymentTotalForPlayClass = 0;

            var returningStudentNonePaymentStudentCounterForPreNursery = 0;
            var returningStudentNonePaymentStudentInfoForPreNursery = "";
            double returningStudentNonePaymentStudentOutstandingPaymentTotalForPreNursery = 0;

            var returningStudentNonePaymentStudentCounterForNursery = 0;
            var returningStudentNonePaymentStudentInfoForNursery = "";
            double returningStudentNonePaymentStudentOutstandingPaymentTotalForNursery = 0;

            var returningStudentNonePaymentStudentCounterForPrimary = 0;
            var returningStudentNonePaymentStudentInforForPrimary = "";
            double returningStudentNonePaymentStudentOutstandingPaymentTotalForPrimary = 0;

            var returningStudentNonePaymentStudentCounterForJss = 0;
            var returningStudentNonePaymentStudentInfoForJss = "";
            double returningStudentNonePaymentStudentOutstandingPaymentTotalForJss = 0;

            var returningStudentNonePaymentStudentCounterForScience = 0;
            var returningStudentNonePaymentStudentInfoForScience = "";
            double returningStudentNonePaymentStudentOutstandingPaymentTotalForScience = 0;

            var returningStudentNonePaymentStudentCounterForArt = 0;
            var returningStudentNonePaymentStudentInfoForArt = "";
            double returningStudentNonePaymentStudentOutstandingPaymentTotalForArt = 0;

            var returningStudentNonePaymentStudentCounterForCommercial = 0;
            var returningStudentNonePaymentStudentInfoForCommercial = "";
            double returningStudentNonePaymentStudentOutstandingPaymentTotalForCommercial = 0;

            var currentTermDiscountRepo =
                ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPaymentDiscount, short>>()
                    .Where(x => x.StudentAcademicProgressHistory.YearlyAndTermlyStatusId == selectedTermId,
                        v => v.StudentAcademicProgressHistory)?.ToList();

            double playClassFreshDiscount = 0;
            double preNurseryFreshDiscount = 0;
            double nurseryFreshDiscount = 0;
            double primaryFreshDiscount = 0;
            double jssFreshDiscount = 0;
            double scienceFreshDiscount = 0;
            double artFreshDiscount = 0;
            double commercialFreshDiscount = 0;

            double playClassRetrnDiscount = 0;
            double preNurseryRetrnDiscount = 0;
            double nurseryRetrnDiscount = 0;
            double primaryRetrnDiscount = 0;
            double jssRetrnDiscount = 0;
            double scienceRetrnDiscount = 0;
            double artRetrnDiscount = 0;
            double commercialRetrnDiscount = 0;
            // List<TxStudentDetails> actualStudentListInTheSelectedTerm;
            var cashPaymentTypeTotal = 0.0;
            var tellerPaymentTypeTotal = 0.0;
            var transferPaymentTypeTotal = 0.0;

            foreach (var eachStudent in listOfStudentDuringTheSelectedTerm)
            {

                var hisClass = eachStudent.StudentDetails.ClassArm.Classs.ClassName;
                double hisFees = 0;
                if (!(hisClass == CurrentClass.Ss1 || hisClass == CurrentClass.Ss2 || hisClass == CurrentClass.Ss3))
                {
                    var className3 = GetClassCategoryStatus(eachStudent.StudentDetails.ClassArm.Classs.ClassName);
                    {
                        hisFees = GetFeesForFreshiers(currentFeesObject, className3); // To avoid Mistake
                    }
                }
                else
                {
                    hisFees = GetFeesForFreshiers(currentFeesObject, eachStudent.StudentDetails.ClassCategory);
                }

                ClassCategoryStatus classCategory;
                if (
                    !(hisClass == CurrentClass.Ss1 || hisClass == CurrentClass.Ss2 ||
                      hisClass == CurrentClass.Ss3))
                {
                    classCategory = GetClassCategoryStatus(hisClass);
                }
                else
                {
                    classCategory = eachStudent.StudentDetails.ClassCategory;
                }
                var anyDiscountForCurrentStudent = currentTermDiscountRepo?.Find(
                    x =>
                        x.StudentAcademicProgressHistory.YearlyAndTermlyStatusId == selectedTermId &&
                        x.StudentAcademicProgressHistory.StudentId == eachStudent.StudentId)?.DiscountAmount ?? 0;

                if (eachStudent.StudentDetails.AdmisnTermId == selectedTermId &&
                    eachStudent.StudentDetails.StudentInitialRegStatus == StudentInitialRegistrationStatus.Fresher)
                {
                    //Indicate freshers during that term and this "eachStudent.StudentDetails.StudentInitialRegStatus == StudentInitialRegistrationStatus.Fresher" handle the case of first time usage 
                    //where the student can be either Fresher or Returning
                    var currentStudentPaymentList =
                        currentTermListOfStudentsPayment.Where(
                            x =>
                                x.StudentAcademicProgressHistoryId == eachStudent.Id &&
                                x.StudentAcademicProgressHistory.YearlyAndTermlyStatusId == selectedTermId).ToList();

                    if (currentStudentPaymentList.Any())
                    {
                        //The student is a freshier student
                        var currentStudentTotalPayment = currentStudentPaymentList.Sum(b => b.Amount);

                        foreach (var item in currentStudentPaymentList) //for the paymentType summarry
                        {
                            if (item.PaymentType == PaymentType.Cash)
                            {
                                cashPaymentTypeTotal = cashPaymentTypeTotal + item.Amount;
                            }
                            else if (item.PaymentType == PaymentType.Teller)
                            {
                                tellerPaymentTypeTotal = tellerPaymentTypeTotal + item.Amount;
                            }
                            else if (item.PaymentType == PaymentType.Transfer)
                            {
                                transferPaymentTypeTotal = transferPaymentTypeTotal + item.Amount;
                            }
                        }

                        if (Math.Abs((currentStudentTotalPayment + anyDiscountForCurrentStudent) - hisFees) < 0.5)
                        {
                            if (classCategory == ClassCategoryStatus.PlayClass)
                            {
                                freshiersWithCompletePaymentCounterForPlayClass =
                                    freshiersWithCompletePaymentCounterForPlayClass + 1;

                                playClassFreshDiscount = playClassFreshDiscount + anyDiscountForCurrentStudent;

                                freshiersWithCompletePaymentTotalAmountForPlayClass =
                                    freshiersWithCompletePaymentTotalAmountForPlayClass +
                                    currentStudentTotalPayment;

                                freshiersInfoWithCompletePaymentForPlayClass =
                                    freshiersInfoWithCompletePaymentForPlayClass != ""
                                        ? freshiersInfoWithCompletePaymentForPlayClass + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" + hisFees + "-" + anyDiscountForCurrentStudent + "~"

                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" + hisFees + "-" + anyDiscountForCurrentStudent;
                                playClassFreshClassCategory = classCategory;

                            }
                            else if (classCategory == ClassCategoryStatus.PreNursery)
                            {
                                freshiersWithCompletePaymentCounterForPreNursery =
                                    freshiersWithCompletePaymentCounterForPreNursery + 1;

                                preNurseryFreshDiscount = preNurseryFreshDiscount + anyDiscountForCurrentStudent;



                                freshiersWithCompletePaymentTotalAmountForPreNursery =
                                    freshiersWithCompletePaymentTotalAmountForPreNursery +
                                    currentStudentTotalPayment;

                                freshiersInfoWithCompletePaymentForPreNursery =
                                    freshiersInfoWithCompletePaymentForPreNursery != ""
                                        ? freshiersInfoWithCompletePaymentForPreNursery + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" + hisFees + "-" + anyDiscountForCurrentStudent + "~"

                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" + hisFees + "-" + anyDiscountForCurrentStudent;
                                preNurseryFreshClassCategory = classCategory;

                            }
                            else if (classCategory == ClassCategoryStatus.Nursery)
                            {
                                freshiersWithCompletePaymentCounteForNursery =
                                    freshiersWithCompletePaymentCounteForNursery + 1;
                                freshiersWithCompletePaymentTotalAmountForNursery =
                                    freshiersWithCompletePaymentTotalAmountForNursery +
                                    currentStudentTotalPayment;
                                nurseryFreshDiscount = nurseryFreshDiscount + anyDiscountForCurrentStudent;

                                freshiersInfoWithCompletePaymentForNursery =
                                    freshiersInfoWithCompletePaymentForNursery != ""
                                        ? freshiersInfoWithCompletePaymentForNursery + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent

                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent;

                                nurseryFreshClassCategory = classCategory;
                            }
                            else if (classCategory == ClassCategoryStatus.Primary)
                            {
                                freshiersWithCompletePaymentCounteForPrimary =
                                    freshiersWithCompletePaymentCounteForPrimary + 1;
                                freshiersWithCompletePaymentTotalAmountForPrimary =
                                    freshiersWithCompletePaymentTotalAmountForPrimary +
                                    currentStudentTotalPayment;
                                primaryFreshDiscount = primaryFreshDiscount + anyDiscountForCurrentStudent;

                                freshiersInfoWithCompletePaymentForPrimary =
                                    freshiersInfoWithCompletePaymentForPrimary != ""
                                        ? freshiersInfoWithCompletePaymentForPrimary + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent

                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent;

                                primaryFreshClassCategory = classCategory;

                            }
                            else if (classCategory == ClassCategoryStatus.Jss)
                            {
                                freshiersWithCompletePaymentCounteForJss =
                                    freshiersWithCompletePaymentCounteForJss + 1;
                                freshiersWithCompletePaymentTotalAmountForJss =
                                    freshiersWithCompletePaymentTotalAmountForJss +
                                    currentStudentTotalPayment;

                                jssFreshDiscount = jssFreshDiscount + anyDiscountForCurrentStudent;

                                freshiersInfoWithCompletePaymentForJss = freshiersInfoWithCompletePaymentForJss != ""
                                    ? freshiersInfoWithCompletePaymentForJss + "," +
                                      currentStudentPaymentList.First()
                                          .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                      currentStudentPaymentList.First()
                                          .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                      currentStudentPaymentList.First()
                                          .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                      currentStudentPaymentList.First()
                                          .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                      "?" +
                                      (byte)
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassName +
                                      "~" + hisFees + "-" +
                                      anyDiscountForCurrentStudent

                                    : currentStudentPaymentList.First()
                                        .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                      currentStudentPaymentList.First()
                                          .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                      currentStudentPaymentList.First()
                                          .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                      currentStudentPaymentList.First()
                                          .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                      "?" +
                                      (byte)
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassName +
                                      "~" + hisFees + "-" +
                                      anyDiscountForCurrentStudent;

                                jssFreshClassCategory = classCategory;
                            }
                            else if (classCategory == ClassCategoryStatus.Science)
                            {
                                freshiersWithCompletePaymentCounteForScience =
                                    freshiersWithCompletePaymentCounteForScience + 1;
                                freshiersWithCompletePaymentTotalAmountForScience =
                                    freshiersWithCompletePaymentTotalAmountForScience +
                                    currentStudentTotalPayment;

                                scienceFreshDiscount = scienceFreshDiscount + anyDiscountForCurrentStudent;

                                freshiersInfoWithCompletePaymentForScience =
                                    freshiersInfoWithCompletePaymentForScience != ""
                                        ? freshiersInfoWithCompletePaymentForScience + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent

                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent;

                                scienceFreshClassCategory = classCategory;

                            }
                            else if (classCategory == ClassCategoryStatus.Art)
                            {
                                freshiersWithCompletePaymentCounteForArt =
                                    freshiersWithCompletePaymentCounteForArt + 1;
                                freshiersWithCompletePaymentTotalAmountForArt =
                                    freshiersWithCompletePaymentTotalAmountForArt +
                                    currentStudentTotalPayment;

                                artFreshDiscount = artFreshDiscount + anyDiscountForCurrentStudent;

                                freshiersInfoWithCompletePaymentForArt = freshiersInfoWithCompletePaymentForArt != ""
                                    ? freshiersInfoWithCompletePaymentForArt + "," +
                                      currentStudentPaymentList.First()
                                          .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                      currentStudentPaymentList.First()
                                          .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                      currentStudentPaymentList.First()
                                          .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                      currentStudentPaymentList.First()
                                          .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                      "?" +
                                      (byte)
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassName +
                                      "~" + hisFees + "-" +
                                      anyDiscountForCurrentStudent

                                    : currentStudentPaymentList.First()
                                        .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                      currentStudentPaymentList.First()
                                          .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                      currentStudentPaymentList.First()
                                          .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                      currentStudentPaymentList.First()
                                          .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                      "?" +
                                      (byte)
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassName +
                                      "~" + hisFees + "-" +
                                      anyDiscountForCurrentStudent;
                                artFreshClassCategory = classCategory;
                            }
                            else if (classCategory == ClassCategoryStatus.Commercial)
                            {
                                freshiersWithCompletePaymentCounteForCommercial =
                                    freshiersWithCompletePaymentCounteForCommercial + 1;
                                freshiersWithCompletePaymentTotalAmountForCommercial =
                                    freshiersWithCompletePaymentTotalAmountForCommercial +
                                    currentStudentTotalPayment;
                                commercialFreshDiscount = commercialFreshDiscount + anyDiscountForCurrentStudent;

                                freshiersInfoWithCompletePaymentForCommercial =
                                    freshiersInfoWithCompletePaymentForCommercial != ""
                                        ? freshiersInfoWithCompletePaymentForCommercial + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent

                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent;

                                commercialFreshClassCategory = classCategory;

                            }
                        }
                        else if (currentStudentTotalPayment < hisFees)
                        {
                            if (classCategory == ClassCategoryStatus.PlayClass)
                            {
                                freshiersWithOutstandingPaymentCounterForPlayClass =
                                    freshiersWithOutstandingPaymentCounterForPlayClass + 1;
                                freshiersWithOutstandingPaymentTotalAmountForPlayClass =
                                    freshiersWithOutstandingPaymentTotalAmountForPlayClass +
                                    (hisFees - currentStudentTotalPayment);
                                freshiersPartPaymentTotalAmountForPlayClass =
                                    freshiersPartPaymentTotalAmountForPlayClass + currentStudentTotalPayment;

                                freshiersInfoWithOutstandingPaymentForPlayClass =
                                    freshiersInfoWithOutstandingPaymentForPlayClass != ""
                                        ? freshiersInfoWithOutstandingPaymentForPlayClass + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment);
                                playClassFreshClassCategory = classCategory;
                            }
                            else if (classCategory == ClassCategoryStatus.PreNursery)
                            {
                                freshiersWithOutstandingPaymentCounterForPreNursery =
                                    freshiersWithOutstandingPaymentCounterForPreNursery + 1;
                                freshiersWithOutstandingPaymentTotalAmountForPreNursery =
                                    freshiersWithOutstandingPaymentTotalAmountForPreNursery +
                                    (hisFees - currentStudentTotalPayment);
                                freshiersPartPaymentTotalAmountForPreNursery =
                                    freshiersPartPaymentTotalAmountForPreNursery + currentStudentTotalPayment;

                                freshiersInfoWithOutstandingPaymentForPreNursery =
                                    freshiersInfoWithOutstandingPaymentForPreNursery != ""
                                        ? freshiersInfoWithOutstandingPaymentForPreNursery + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment);
                                preNurseryFreshClassCategory = classCategory;
                            }
                            else if (classCategory == ClassCategoryStatus.Nursery)
                            {
                                freshiersWithOutstandingPaymentCounterForNursery =
                                    freshiersWithOutstandingPaymentCounterForNursery + 1;
                                freshiersWithOutstandingPaymentTotalAmountForNursery =
                                    freshiersWithOutstandingPaymentTotalAmountForNursery +
                                    (hisFees - currentStudentTotalPayment);
                                freshiersPartPaymentTotalAmountForNursery = freshiersPartPaymentTotalAmountForNursery +
                                                                            currentStudentTotalPayment;

                                freshiersInfoWithOutstandingPaymentForNursery =
                                    freshiersInfoWithOutstandingPaymentForNursery != ""
                                        ? freshiersInfoWithOutstandingPaymentForNursery + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment);
                                nurseryFreshClassCategory = classCategory;
                            }
                            else if (classCategory == ClassCategoryStatus.Primary)
                            {
                                freshiersWithOutstandingPaymentCounterForPrimary =
                                    freshiersWithOutstandingPaymentCounterForPrimary + 1;
                                freshiersWithOutstandingPaymentTotalAmountForPrimary =
                                    freshiersWithOutstandingPaymentTotalAmountForPrimary +
                                    (hisFees - currentStudentTotalPayment);
                                freshiersPartPaymentTotalAmountForPrimary = freshiersPartPaymentTotalAmountForPrimary +
                                                                            currentStudentTotalPayment;

                                freshiersInfoWithOutstandingPaymentForPrimary =
                                    freshiersInfoWithOutstandingPaymentForPrimary != ""
                                        ? freshiersInfoWithOutstandingPaymentForPrimary + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment);
                                primaryFreshClassCategory = classCategory;
                            }
                            else if (classCategory == ClassCategoryStatus.Jss)
                            {
                                freshiersWithOutstandingPaymentCounterForJss =
                                    freshiersWithOutstandingPaymentCounterForJss + 1;
                                freshiersWithOutstandingPaymentTotalAmountForJss =
                                    freshiersWithOutstandingPaymentTotalAmountForJss +
                                    (hisFees - currentStudentTotalPayment);
                                freshiersPartPaymentTotalAmountForJss = freshiersPartPaymentTotalAmountForJss +
                                                                        currentStudentTotalPayment;

                                freshiersInfoWithOutstandingPaymentForJss =
                                    freshiersInfoWithOutstandingPaymentForJss != ""
                                        ? freshiersInfoWithOutstandingPaymentForJss + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment);
                                jssFreshClassCategory = classCategory;
                            }
                            else if (classCategory == ClassCategoryStatus.Science)
                            {
                                freshiersWithOutstandingPaymentCounterForScience =
                                    freshiersWithOutstandingPaymentCounterForScience + 1;
                                freshiersWithOutstandingPaymentTotalAmountForScience =
                                    freshiersWithOutstandingPaymentTotalAmountForScience +
                                    (hisFees - currentStudentTotalPayment);
                                freshiersPartPaymentTotalAmountForScience = freshiersPartPaymentTotalAmountForScience +
                                                                            currentStudentTotalPayment;

                                freshiersInfoWithOutstandingPaymentForScience =
                                    freshiersInfoWithOutstandingPaymentForScience != ""
                                        ? freshiersInfoWithOutstandingPaymentForScience + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment);
                                scienceFreshClassCategory = classCategory;
                            }
                            else if (classCategory == ClassCategoryStatus.Art)
                            {
                                freshiersWithOutstandingPaymentCounterForArt =
                                    freshiersWithOutstandingPaymentCounterForArt + 1;
                                freshiersWithOutstandingPaymentTotalAmountForArt =
                                    freshiersWithOutstandingPaymentTotalAmountForArt +
                                    (hisFees - currentStudentTotalPayment);
                                freshiersPartPaymentTotalAmountForArt = freshiersPartPaymentTotalAmountForArt +
                                                                        currentStudentTotalPayment;

                                freshiersInfoWithOutstandingPaymentForArt =
                                    freshiersInfoWithOutstandingPaymentForArt != ""
                                        ? freshiersInfoWithOutstandingPaymentForArt + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment);
                                artFreshClassCategory = classCategory;
                            }

                            else if (classCategory == ClassCategoryStatus.Commercial)
                            {
                                freshiersWithOutstandingPaymentCounterForCommercial =
                                    freshiersWithOutstandingPaymentCounterForCommercial + 1;
                                freshiersWithOutstandingPaymentTotalAmountForCommercial =
                                    freshiersWithOutstandingPaymentTotalAmountForCommercial +
                                    (hisFees - currentStudentTotalPayment);
                                freshiersPartPaymentTotalAmountForCommercial =
                                    freshiersPartPaymentTotalAmountForCommercial + currentStudentTotalPayment;

                                freshiersInfoWithOutstandingPaymentForCommercial =
                                    freshiersInfoWithOutstandingPaymentForCommercial != ""
                                        ? freshiersInfoWithOutstandingPaymentForCommercial + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName +
                                          "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment);
                                commercialFreshClassCategory = classCategory;
                            }
                        }

                    }
                    else //Fresher with none payment
                    {

                        if (
                            !(hisClass == CurrentClass.Ss1 || hisClass == CurrentClass.Ss2 ||
                              hisClass == CurrentClass.Ss3))
                        {
                            classCategory = GetClassCategoryStatus(hisClass);
                        }
                        else
                        {
                            classCategory = eachStudent.StudentDetails.ClassCategory;
                        }
                        if (classCategory == ClassCategoryStatus.PlayClass)
                        {
                            freshierNonePaymentStudentCounterForPlayClass =
                                freshierNonePaymentStudentCounterForPlayClass + 1;
                            freshierNonePaymentStudentOutstandingPaymentTotalForPlayClass =
                                freshierNonePaymentStudentOutstandingPaymentTotalForPlayClass + freshPlayClass;

                            freshierNonePaymentStudentInfoForPlayClass = freshierNonePaymentStudentInfoForPlayClass !=
                                                                         ""
                                ? freshierNonePaymentStudentInfoForPlayClass + "," + eachStudent.StudentDetails.IdNo +
                                  "~" +
                                  eachStudent.StudentDetails.User.FullName +
                                  "~" + eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                  eachStudent.StudentDetails.ClassArm.ArmName +
                                  "?" + (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                  freshPlayClass
                                : eachStudent.StudentDetails.IdNo + "~" + eachStudent.StudentDetails.User.FullName + "~" +
                                  eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                  eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                  (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                  freshPlayClass;
                            playClassFreshClassCategory = classCategory;
                        }
                        else if (classCategory == ClassCategoryStatus.PreNursery)
                        {
                            freshierNonePaymentStudentCounterForPreNursery =
                                freshierNonePaymentStudentCounterForPreNursery + 1;
                            freshierNonePaymentStudentOutstandingPaymentTotalForPreNursery =
                                freshierNonePaymentStudentOutstandingPaymentTotalForPreNursery + freshPreNursery;

                            freshierNonePaymentStudentInfoForPreNursery = freshierNonePaymentStudentInfoForPreNursery !=
                                                                          ""
                                ? freshierNonePaymentStudentInfoForPreNursery + "," + eachStudent.StudentDetails.IdNo +
                                  "~" +
                                  eachStudent.StudentDetails.User.FullName +
                                  "~" + eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                  eachStudent.StudentDetails.ClassArm.ArmName +
                                  "?" + (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                  freshPreNursery
                                : eachStudent.StudentDetails.IdNo + "~" + eachStudent.StudentDetails.User.FullName + "~" +
                                  eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                  eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                  (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                  freshPreNursery;
                            preNurseryFreshClassCategory = classCategory;
                        }
                        else if (classCategory == ClassCategoryStatus.Nursery)
                        {
                            freshierNonePaymentStudentCounterForNursery = freshierNonePaymentStudentCounterForNursery +
                                                                          1;
                            freshierNonePaymentStudentOutstandingPaymentTotalForNursery =
                                freshierNonePaymentStudentOutstandingPaymentTotalForNursery + freshNursery;

                            freshierNonePaymentStudentInfoForNursery = freshierNonePaymentStudentInfoForNursery != ""
                                ? freshierNonePaymentStudentInfoForNursery + "," + eachStudent.StudentDetails.IdNo + "~" +
                                  eachStudent.StudentDetails.User.FullName +
                                  "~" + eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                  eachStudent.StudentDetails.ClassArm.ArmName +
                                  "?" + (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" + freshNursery
                                : eachStudent.StudentDetails.IdNo + "~" + eachStudent.StudentDetails.User.FullName + "~" +
                                  eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                  eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                  (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                  freshNursery;
                            nurseryFreshClassCategory = classCategory;
                        }
                        else if (classCategory == ClassCategoryStatus.Primary)
                        {
                            freshierNonePaymentStudentCounterForPrimary = freshierNonePaymentStudentCounterForPrimary +
                                                                          1;
                            freshierNonePaymentStudentOutstandingPaymentTotalForPrimary =
                                freshierNonePaymentStudentOutstandingPaymentTotalForPrimary + freshPrimary;

                            freshierNonePaymentStudentInfoForPrimary = freshierNonePaymentStudentInfoForPrimary != ""
                                ? freshierNonePaymentStudentInfoForPrimary + "," + eachStudent.StudentDetails.IdNo + "~" +
                                  eachStudent.StudentDetails.User.FullName +
                                  "~" + eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                  eachStudent.StudentDetails.ClassArm.ArmName +
                                  "?" + (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" + freshPrimary
                                : eachStudent.StudentDetails.IdNo + "~" + eachStudent.StudentDetails.User.FullName + "~" +
                                  eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                  eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                  (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                  freshPrimary;
                            primaryFreshClassCategory = classCategory;
                        }

                        else if (classCategory == ClassCategoryStatus.Jss)
                        {
                            freshierNonePaymentStudentCounterForJss = freshierNonePaymentStudentCounterForJss + 1;
                            freshierNonePaymentStudentOutstandingPaymentTotalForJss =
                                freshierNonePaymentStudentOutstandingPaymentTotalForJss + freshJss;

                            freshierNonePaymentStudentInfoForJss = freshierNonePaymentStudentInfoForJss != ""
                                ? freshierNonePaymentStudentInfoForJss + "," + eachStudent.StudentDetails.IdNo + "~" +
                                  eachStudent.StudentDetails.User.FullName +
                                  "~" + eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                  eachStudent.StudentDetails.ClassArm.ArmName +
                                  "?" + (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" + freshJss
                                : eachStudent.StudentDetails.IdNo + "~" + eachStudent.StudentDetails.User.FullName + "~" +
                                  eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                  eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                  (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                  freshJss;
                            jssFreshClassCategory = classCategory;
                        }

                        else if (classCategory == ClassCategoryStatus.Science)
                        {
                            freshierNonePaymentStudentCounterForScience = freshierNonePaymentStudentCounterForScience +
                                                                          1;
                            freshierNonePaymentStudentOutstandingPaymentTotalForScience =
                                freshierNonePaymentStudentOutstandingPaymentTotalForScience + freshScience;

                            freshierNonePaymentStudentInfoForScience = freshierNonePaymentStudentInfoForScience != ""
                                ? freshierNonePaymentStudentInfoForScience + "," + eachStudent.StudentDetails.IdNo + "~" +
                                  eachStudent.StudentDetails.User.FullName +
                                  "~" + eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                  eachStudent.StudentDetails.ClassArm.ArmName +
                                  "?" + (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" + freshScience
                                : eachStudent.StudentDetails.IdNo + "~" + eachStudent.StudentDetails.User.FullName + "~" +
                                  eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                  eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                  (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                  freshScience;
                            scienceFreshClassCategory = classCategory;
                        }

                        else if (classCategory == ClassCategoryStatus.Art)
                        {
                            freshierNonePaymentStudentCounterForArt = freshierNonePaymentStudentCounterForArt + 1;
                            freshierNonePaymentStudentOutstandingPaymentTotalForArt =
                                freshierNonePaymentStudentOutstandingPaymentTotalForArt + freshArt;

                            freshierNonePaymentStudentInfoForArt = freshierNonePaymentStudentInfoForArt != ""
                                ? freshierNonePaymentStudentInfoForArt + "," + eachStudent.StudentDetails.IdNo + "~" +
                                  eachStudent.StudentDetails.User.FullName +
                                  "~" + eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                  eachStudent.StudentDetails.ClassArm.ArmName +
                                  "?" + (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" + freshArt
                                : eachStudent.StudentDetails.IdNo + "~" + eachStudent.StudentDetails.User.FullName + "~" +
                                  eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                  eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                  (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                  freshArt;
                            artFreshClassCategory = classCategory;
                        }

                        if (classCategory == ClassCategoryStatus.Commercial)
                        {
                            freshierNonePaymentStudentCounterForCommercial =
                                freshierNonePaymentStudentCounterForCommercial + 1;
                            freshierNonePaymentStudentOutstandingPaymentTotalForCommercial =
                                freshierNonePaymentStudentOutstandingPaymentTotalForCommercial + freshCommercial;

                            freshierNonePaymentStudentInfoForCommercial = freshierNonePaymentStudentInfoForCommercial !=
                                                                          ""
                                ? freshierNonePaymentStudentInfoForCommercial + "," + eachStudent.StudentDetails.IdNo +
                                  "~" +
                                  eachStudent.StudentDetails.User.FullName +
                                  "~" + eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                  eachStudent.StudentDetails.ClassArm.ArmName +
                                  "?" + (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                  freshCommercial
                                : eachStudent.StudentDetails.IdNo + "~" + eachStudent.StudentDetails.User.FullName + "~" +
                                  eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                  eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                  (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                  freshCommercial;
                            commercialFreshClassCategory = classCategory;
                        }
                    }
                }
                else //Indicate returning student during that term
                {
                    var currentStudentPaymentList = currentTermListOfStudentsPayment.Where(
                        x =>
                            x.StudentAcademicProgressHistoryId == eachStudent.Id &&
                            x.StudentAcademicProgressHistory.YearlyAndTermlyStatusId == selectedTermId)?.ToList();

                    if (currentStudentPaymentList != null && currentStudentPaymentList.Any())
                    {
                        foreach (var item in currentStudentPaymentList) //for the paymentType summarry
                        {
                            if (item.PaymentType == PaymentType.Cash)
                            {
                                cashPaymentTypeTotal = cashPaymentTypeTotal + item.Amount;
                            }
                            else if (item.PaymentType == PaymentType.Teller)
                            {
                                tellerPaymentTypeTotal = tellerPaymentTypeTotal + item.Amount;
                            }
                            else if (item.PaymentType == PaymentType.Transfer)
                            {
                                transferPaymentTypeTotal = transferPaymentTypeTotal + item.Amount;
                            }
                        }
                        if (
                            !(hisClass == CurrentClass.Ss1 || hisClass == CurrentClass.Ss2 ||
                              hisClass == CurrentClass.Ss3))
                        {
                            var className3 =
                                GetClassCategoryStatus(
                                    currentStudentPaymentList.First()
                                        .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassName);
                            {
                                hisFees = GetFeesForReturningStudent(currentFeesObject, className3); // To avoid Mistake
                            }
                        }
                        else
                        {
                            hisFees = GetFeesForReturningStudent(currentFeesObject,
                                currentStudentPaymentList.First()
                                    .StudentAcademicProgressHistory.StudentDetails.ClassCategory);
                        }

                        if (
                            !(hisClass == CurrentClass.Ss1 || hisClass == CurrentClass.Ss2 ||
                              hisClass == CurrentClass.Ss3))
                        {
                            classCategory = GetClassCategoryStatus(hisClass);
                        }
                        else
                        {
                            classCategory =
                                currentStudentPaymentList.First()
                                    .StudentAcademicProgressHistory.StudentDetails.ClassCategory;
                        }
                        var currentStudentTotalPayment = currentStudentPaymentList.Sum(b => b.Amount);

                        if (Math.Abs((currentStudentTotalPayment + anyDiscountForCurrentStudent) - hisFees) < 0.7)
                        {
                            if (classCategory ==
                                ClassCategoryStatus.PlayClass)
                            {
                                returningStudentsWithCompletePaymentCounterForPlayClass =
                                    returningStudentsWithCompletePaymentCounterForPlayClass + 1;
                                returningStudentsWithCompletePaymentTotalAmountForPlayClass =
                                    returningStudentsWithCompletePaymentTotalAmountForPlayClass +
                                    currentStudentTotalPayment;

                                playClassRetrnDiscount = playClassRetrnDiscount + anyDiscountForCurrentStudent;

                                returningStudentsInfoWithCompletePaymentForPlayClass =
                                    returningStudentsInfoWithCompletePaymentForPlayClass != ""
                                        ? returningStudentsInfoWithCompletePaymentForPlayClass + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent

                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent;
                                playClassRetrnClassCategory = classCategory;

                            }
                            else if (classCategory ==
                                     ClassCategoryStatus.PreNursery)
                            {
                                returningStudentsWithCompletePaymentCounterForPreNursery =
                                    returningStudentsWithCompletePaymentCounterForPreNursery + 1;
                                returningStudentsWithCompletePaymentTotalAmountForPreNursery =
                                    returningStudentsWithCompletePaymentTotalAmountForPreNursery +
                                    currentStudentTotalPayment;

                                preNurseryRetrnDiscount = preNurseryRetrnDiscount + anyDiscountForCurrentStudent;

                                returningStudentsInfoWithCompletePaymentForPreNursery =
                                    returningStudentsInfoWithCompletePaymentForPreNursery != ""
                                        ? returningStudentsInfoWithCompletePaymentForPreNursery + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent

                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent;
                                preNurseryRetrnClassCategory = classCategory;

                            }
                            else if (classCategory ==
                                     ClassCategoryStatus.Nursery)
                            {
                                returningStudentsWithCompletePaymentCounteForNursery =
                                    returningStudentsWithCompletePaymentCounteForNursery + 1;
                                returningStudentsWithCompletePaymentTotalAmountForNursery =
                                    returningStudentsWithCompletePaymentTotalAmountForNursery +
                                    currentStudentTotalPayment;

                                nurseryRetrnDiscount = nurseryRetrnDiscount + anyDiscountForCurrentStudent;

                                returningStudentsInfoWithCompletePaymentForNursery =
                                    returningStudentsInfoWithCompletePaymentForNursery != ""
                                        ? returningStudentsInfoWithCompletePaymentForNursery + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent

                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent;
                                nurseryRetrnClassCategory = classCategory;

                            }
                            else if (classCategory ==
                                     ClassCategoryStatus.Primary)
                            {
                                returningStudentsWithCompletePaymentCounteForPrimary =
                                    returningStudentsWithCompletePaymentCounteForPrimary + 1;
                                returningStudentsWithCompletePaymentTotalAmountForPrimary =
                                    returningStudentsWithCompletePaymentTotalAmountForPrimary +
                                    currentStudentTotalPayment;

                                primaryRetrnDiscount = primaryRetrnDiscount + anyDiscountForCurrentStudent;

                                returningStudentsInfoWithCompletePaymentForPrimary =
                                    returningStudentsInfoWithCompletePaymentForPrimary != ""
                                        ? returningStudentsInfoWithCompletePaymentForPrimary + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent;
                                primaryRetrnClassCategory = classCategory;

                            }
                            else if (classCategory ==
                                     ClassCategoryStatus.Jss)
                            {
                                returningStudentsWithCompletePaymentCounteForJss =
                                    returningStudentsWithCompletePaymentCounteForJss + 1;
                                returningStudentsWithCompletePaymentTotalAmountForJss =
                                    returningStudentsWithCompletePaymentTotalAmountForJss +
                                    currentStudentTotalPayment;

                                jssRetrnDiscount = jssRetrnDiscount + anyDiscountForCurrentStudent;

                                returningStudentsInfoWithCompletePaymentForJss =
                                    returningStudentsInfoWithCompletePaymentForJss != ""
                                        ? returningStudentsInfoWithCompletePaymentForJss + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent;
                                jssRetrnClassCategory = classCategory;

                            }
                            else if (classCategory ==
                                     ClassCategoryStatus.Science)
                            {
                                returningStudentsWithCompletePaymentCounteForScience =
                                    returningStudentsWithCompletePaymentCounteForScience + 1;
                                returningStudentsWithCompletePaymentTotalAmountForScience =
                                    returningStudentsWithCompletePaymentTotalAmountForScience +
                                    currentStudentTotalPayment;

                                scienceRetrnDiscount = scienceRetrnDiscount + anyDiscountForCurrentStudent;

                                returningStudentsInfoWithCompletePaymentForScience =
                                    returningStudentsInfoWithCompletePaymentForScience != ""
                                        ? returningStudentsInfoWithCompletePaymentForScience + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent;
                                scienceRetrnClassCategory =
                                    currentStudentPaymentList.First()
                                        .StudentAcademicProgressHistory.StudentDetails.ClassCategory;

                            }
                            else if (classCategory ==
                                     ClassCategoryStatus.Art)
                            {
                                returningStudentsWithCompletePaymentCounteForArt =
                                    returningStudentsWithCompletePaymentCounteForArt + 1;
                                returningStudentsWithCompletePaymentTotalAmountForArt =
                                    returningStudentsWithCompletePaymentTotalAmountForArt +
                                    currentStudentTotalPayment;

                                artRetrnDiscount = artRetrnDiscount + anyDiscountForCurrentStudent;

                                returningStudentsInfoWithCompletePaymentForArt =
                                    returningStudentsInfoWithCompletePaymentForArt != ""
                                        ? returningStudentsInfoWithCompletePaymentForArt + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent;
                                artRetrnClassCategory =
                                    currentStudentPaymentList.First()
                                        .StudentAcademicProgressHistory.StudentDetails.ClassCategory;

                            }
                            else if (
                                classCategory ==
                                ClassCategoryStatus.Commercial)
                            {
                                returningStudentsWithCompletePaymentCounteForCommercial =
                                    returningStudentsWithCompletePaymentCounteForCommercial + 1;
                                returningStudentsWithCompletePaymentTotalAmountForCommercial =
                                    returningStudentsWithCompletePaymentTotalAmountForCommercial +
                                    currentStudentTotalPayment;

                                commercialRetrnDiscount = commercialRetrnDiscount + anyDiscountForCurrentStudent;

                                returningStudentsInfoWithCompletePaymentForCommercial =
                                    returningStudentsInfoWithCompletePaymentForCommercial != ""
                                        ? returningStudentsInfoWithCompletePaymentForCommercial + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName + "~" + hisFees + "-" +
                                          anyDiscountForCurrentStudent;
                                commercialRetrnClassCategory =
                                    currentStudentPaymentList.First()
                                        .StudentAcademicProgressHistory.StudentDetails.ClassCategory;

                            }
                        }
                        else if (currentStudentTotalPayment < hisFees)
                        {
                            if (classCategory ==
                                ClassCategoryStatus.PlayClass)
                            {
                                returningStudentsWithOutstandingPaymentCounterForPlayClass =
                                    returningStudentsWithOutstandingPaymentCounterForPlayClass + 1;
                                returningStudentsWithOutstandingPaymentTotalAmountForPlayClass =
                                    returningStudentsWithOutstandingPaymentTotalAmountForPlayClass +
                                    (hisFees - currentStudentTotalPayment);
                                returningStudentsPartPaymentTotalAmountForPlayClass =
                                    returningStudentsPartPaymentTotalAmountForPlayClass + currentStudentTotalPayment;

                                returningStudentsInfoWithOutstandingPaymentForPlayClass =
                                    returningStudentsInfoWithOutstandingPaymentForPlayClass != ""
                                        ? returningStudentsInfoWithOutstandingPaymentForPlayClass + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment);
                                playClassRetrnClassCategory = classCategory;
                            }
                            else if (classCategory ==
                                     ClassCategoryStatus.PreNursery)
                            {
                                returningStudentsWithOutstandingPaymentCounterForPreNursery =
                                    returningStudentsWithOutstandingPaymentCounterForPreNursery + 1;
                                returningStudentsWithOutstandingPaymentTotalAmountForPreNursery =
                                    returningStudentsWithOutstandingPaymentTotalAmountForPreNursery +
                                    (hisFees - currentStudentTotalPayment);
                                returningStudentsPartPaymentTotalAmountForPreNursery =
                                    returningStudentsPartPaymentTotalAmountForPreNursery + currentStudentTotalPayment;

                                returningStudentsInfoWithOutstandingPaymentForPreNursery =
                                    returningStudentsInfoWithOutstandingPaymentForPreNursery != ""
                                        ? returningStudentsInfoWithOutstandingPaymentForPreNursery + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment);
                                preNurseryRetrnClassCategory = classCategory;
                            }
                            else if (classCategory ==
                                     ClassCategoryStatus.Nursery)
                            {
                                returningStudentsWithOutstandingPaymentCounterForNursery =
                                    returningStudentsWithOutstandingPaymentCounterForNursery + 1;
                                returningStudentsWithOutstandingPaymentTotalAmountForNursery =
                                    returningStudentsWithOutstandingPaymentTotalAmountForNursery +
                                    (hisFees - currentStudentTotalPayment);
                                returningStudentsPartPaymentTotalAmountForNursery =
                                    returningStudentsPartPaymentTotalAmountForNursery + currentStudentTotalPayment;

                                returningStudentsInfoWithOutstandingPaymentForNursery =
                                    returningStudentsInfoWithOutstandingPaymentForNursery != ""
                                        ? returningStudentsInfoWithOutstandingPaymentForNursery + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment);
                                nurseryRetrnClassCategory = classCategory;
                            }
                            else if (classCategory ==
                                     ClassCategoryStatus.Primary)
                            {
                                returningStudentsWithOutstandingPaymentCounterForPrimary =
                                    returningStudentsWithOutstandingPaymentCounterForPrimary + 1;
                                returningStudentsWithOutstandingPaymentTotalAmountForPrimary =
                                    returningStudentsWithOutstandingPaymentTotalAmountForPrimary +
                                    (hisFees - currentStudentTotalPayment);
                                returningStudentssPartPaymentTotalAmountForPrimary =
                                    returningStudentssPartPaymentTotalAmountForPrimary + currentStudentTotalPayment;

                                returningStudentsInfoWithOutstandingPaymentForPrimary =
                                    returningStudentsInfoWithOutstandingPaymentForPrimary != ""
                                        ? returningStudentsInfoWithOutstandingPaymentForPrimary + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment);
                                primaryRetrnClassCategory = classCategory;
                            }
                            else if (classCategory ==
                                     ClassCategoryStatus.Jss)
                            {
                                returningStudentsWithOutstandingPaymentCounterForJss =
                                    returningStudentsWithOutstandingPaymentCounterForJss + 1;
                                returningStudentsWithOutstandingPaymentTotalAmountForJss =
                                    returningStudentsWithOutstandingPaymentTotalAmountForJss +
                                    (hisFees - currentStudentTotalPayment);
                                returningStudentsPartPaymentTotalAmountForJss =
                                    returningStudentsPartPaymentTotalAmountForJss + currentStudentTotalPayment;

                                returningStudentsInfoWithOutstandingPaymentForJss =
                                    returningStudentsInfoWithOutstandingPaymentForJss != ""
                                        ? returningStudentsInfoWithOutstandingPaymentForJss + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment);
                                jssRetrnClassCategory = classCategory;
                            }
                            else if (classCategory ==
                                     ClassCategoryStatus.Science)
                            {
                                returningStudentsWithOutstandingPaymentCounterForScience =
                                    returningStudentsWithOutstandingPaymentCounterForScience + 1;
                                returningStudentsWithOutstandingPaymentTotalAmountForScience =
                                    returningStudentsWithOutstandingPaymentTotalAmountForScience +
                                    (hisFees - currentStudentTotalPayment);
                                returningStudentsPartPaymentTotalAmountForScience =
                                    returningStudentsPartPaymentTotalAmountForScience + currentStudentTotalPayment;

                                returningStudentsInfoWithOutstandingPaymentForScience =
                                    returningStudentsInfoWithOutstandingPaymentForScience != ""
                                        ? returningStudentsInfoWithOutstandingPaymentForScience + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment);
                                scienceRetrnClassCategory =
                                    currentStudentPaymentList.First()
                                        .StudentAcademicProgressHistory.StudentDetails.ClassCategory;
                            }
                            else if (classCategory ==
                                     ClassCategoryStatus.Art)
                            {
                                returningStudentsWithOutstandingPaymentCounterForArt =
                                    returningStudentsWithOutstandingPaymentCounterForArt + 1;
                                returningStudentsWithOutstandingPaymentTotalAmountForArt =
                                    returningStudentsWithOutstandingPaymentTotalAmountForArt +
                                    (hisFees - currentStudentTotalPayment);
                                returningStudentsPartPaymentTotalAmountForArt =
                                    returningStudentsPartPaymentTotalAmountForArt + currentStudentTotalPayment;

                                returningStudentsInfoWithOutstandingPaymentForArt =
                                    returningStudentsInfoWithOutstandingPaymentForArt != ""
                                        ? returningStudentsInfoWithOutstandingPaymentForArt + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment);
                                artRetrnClassCategory =
                                    currentStudentPaymentList.First()
                                        .StudentAcademicProgressHistory.StudentDetails.ClassCategory;
                            }

                            else if (
                                classCategory ==
                                ClassCategoryStatus.Commercial)
                            {
                                returningStudentsWithOutstandingPaymentCounterForCommercial =
                                    returningStudentsWithOutstandingPaymentCounterForCommercial + 1;
                                returningStudentsWithOutstandingPaymentTotalAmountForCommercial =
                                    returningStudentsWithOutstandingPaymentTotalAmountForCommercial +
                                    (hisFees - currentStudentTotalPayment);
                                returningStudentsPartPaymentTotalAmountForCommercial =
                                    returningStudentsPartPaymentTotalAmountForCommercial + currentStudentTotalPayment;

                                returningStudentsInfoWithOutstandingPaymentForCommercial =
                                    returningStudentsInfoWithOutstandingPaymentForCommercial != ""
                                        ? returningStudentsInfoWithOutstandingPaymentForCommercial + "," +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment)
                                        : currentStudentPaymentList.First()
                                            .StudentAcademicProgressHistory.StudentDetails.IdNo + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.User.FullName + "~" +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs.ClassLabel +
                                          currentStudentPaymentList.First()
                                              .StudentAcademicProgressHistory.StudentDetails.ClassArm.ArmName + "?" +
                                          (byte)
                                              currentStudentPaymentList.First()
                                                  .StudentAcademicProgressHistory.StudentDetails.ClassArm.Classs
                                                  .ClassName +
                                          "~" +
                                          currentStudentTotalPayment + "X" + (hisFees - currentStudentTotalPayment);
                                commercialRetrnClassCategory =
                                    currentStudentPaymentList.First()
                                        .StudentAcademicProgressHistory.StudentDetails.ClassCategory;
                            }
                        }
                    }
                    else
                    {
                        var classCatStatus = ClassCategoryStatus.None;
                        double hisSchoolFees = 0;
                        var classs = eachStudent.StudentDetails.ClassArm.Classs.ClassName;
                        if (!(classs == CurrentClass.Ss1 || classs == CurrentClass.Ss2 || classs == CurrentClass.Ss3))
                        {
                            classCatStatus = GetClassCategoryStatus(eachStudent.StudentDetails.ClassArm.Classs.ClassName);
                            {
                                hisSchoolFees = GetFeesForReturningStudent(currentFeesObject, classCatStatus);
                                // To avoid Mistake
                            }
                        }
                        else
                        {
                            classCatStatus = eachStudent.StudentDetails.ClassCategory;
                            hisSchoolFees = GetFeesForReturningStudent(currentFeesObject, classCatStatus);
                        }
                        if (eachStudent.StudentDetails.ClassCategory == ClassCategoryStatus.PlayClass)
                        {
                            returningStudentNonePaymentStudentCounterForPlayClass =
                                returningStudentNonePaymentStudentCounterForPlayClass + 1;
                            returningStudentNonePaymentStudentOutstandingPaymentTotalForPlayClass =
                                returningStudentNonePaymentStudentOutstandingPaymentTotalForPlayClass + hisSchoolFees;

                            returningStudentNonePaymentStudentInfoForPlayClass =
                                returningStudentNonePaymentStudentInfoForPlayClass != ""
                                    ? returningStudentNonePaymentStudentInfoForPlayClass + "," +
                                      eachStudent.StudentDetails.IdNo + "~" +
                                      eachStudent.StudentDetails.User.FullName + "~" +
                                      eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                      eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                      (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                      hisSchoolFees
                                    : eachStudent.StudentDetails.IdNo + "~" +
                                      eachStudent.StudentDetails.User.FullName + "~" +
                                      eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                      eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                      (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                      hisSchoolFees;
                            playClassRetrnClassCategory = classCatStatus;
                        }

                        else if (classCatStatus == ClassCategoryStatus.PreNursery)
                        {
                            returningStudentNonePaymentStudentCounterForPreNursery =
                                returningStudentNonePaymentStudentCounterForPreNursery + 1;
                            returningStudentNonePaymentStudentOutstandingPaymentTotalForPreNursery =
                                returningStudentNonePaymentStudentOutstandingPaymentTotalForPreNursery + hisSchoolFees;

                            returningStudentNonePaymentStudentInfoForPreNursery =
                                returningStudentNonePaymentStudentInfoForPreNursery != ""
                                    ? returningStudentNonePaymentStudentInfoForPreNursery + "," +
                                      eachStudent.StudentDetails.IdNo + "~" +
                                      eachStudent.StudentDetails.User.FullName + "~" +
                                      eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                      eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                      (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                      hisSchoolFees
                                    : eachStudent.StudentDetails.IdNo + "~" +
                                      eachStudent.StudentDetails.User.FullName + "~" +
                                      eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                      eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                      (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                      hisSchoolFees;
                            preNurseryRetrnClassCategory = classCatStatus;
                        }

                        else if (classCatStatus == ClassCategoryStatus.Nursery)
                        {
                            returningStudentNonePaymentStudentCounterForNursery =
                                returningStudentNonePaymentStudentCounterForNursery + 1;
                            returningStudentNonePaymentStudentOutstandingPaymentTotalForNursery =
                                returningStudentNonePaymentStudentOutstandingPaymentTotalForNursery + hisSchoolFees;

                            returningStudentNonePaymentStudentInfoForNursery =
                                returningStudentNonePaymentStudentInfoForNursery != ""
                                    ? returningStudentNonePaymentStudentInfoForNursery + "," +
                                      eachStudent.StudentDetails.IdNo + "~" +
                                      eachStudent.StudentDetails.User.FullName + "~" +
                                      eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                      eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                      (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                      hisSchoolFees
                                    : eachStudent.StudentDetails.IdNo + "~" +
                                      eachStudent.StudentDetails.User.FullName + "~" +
                                      eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                      eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                      (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                      hisSchoolFees;
                            nurseryRetrnClassCategory = classCatStatus;
                        }

                        else if (classCatStatus == ClassCategoryStatus.Primary)
                        {
                            returningStudentNonePaymentStudentCounterForPrimary =
                                returningStudentNonePaymentStudentCounterForPrimary + 1;
                            returningStudentNonePaymentStudentOutstandingPaymentTotalForPrimary =
                                returningStudentNonePaymentStudentOutstandingPaymentTotalForPrimary + hisSchoolFees;

                            returningStudentNonePaymentStudentInforForPrimary =
                                returningStudentNonePaymentStudentInforForPrimary != ""
                                    ? returningStudentNonePaymentStudentInforForPrimary + "," +
                                      eachStudent.StudentDetails.IdNo + "~" +
                                      eachStudent.StudentDetails.User.FullName + "~" +
                                      eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                      eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                      (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                      hisSchoolFees
                                    : eachStudent.StudentDetails.IdNo + "~" +
                                      eachStudent.StudentDetails.User.FullName + "~" +
                                      eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                      eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                      (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                      hisSchoolFees;
                            primaryRetrnClassCategory = classCatStatus;
                        }

                        else if (classCatStatus == ClassCategoryStatus.Jss)
                        {
                            returningStudentNonePaymentStudentCounterForJss =
                                returningStudentNonePaymentStudentCounterForJss + 1;
                            returningStudentNonePaymentStudentOutstandingPaymentTotalForJss =
                                returningStudentNonePaymentStudentOutstandingPaymentTotalForJss + hisSchoolFees;

                            returningStudentNonePaymentStudentInfoForJss =
                                returningStudentNonePaymentStudentInfoForJss != ""
                                    ? returningStudentNonePaymentStudentInfoForJss + "," +
                                      eachStudent.StudentDetails.IdNo + "~" +
                                      eachStudent.StudentDetails.User.FullName + "~" +
                                      eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                      eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                      (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                      hisSchoolFees
                                    : eachStudent.StudentDetails.IdNo + "~" +
                                      eachStudent.StudentDetails.User.FullName + "~" +
                                      eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                      eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                      (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                      hisSchoolFees;
                            jssRetrnClassCategory = classCatStatus;
                        }

                        else if (classCatStatus == ClassCategoryStatus.Science)
                        {
                            returningStudentNonePaymentStudentCounterForScience =
                                returningStudentNonePaymentStudentCounterForScience + 1;
                            returningStudentNonePaymentStudentOutstandingPaymentTotalForScience =
                                returningStudentNonePaymentStudentOutstandingPaymentTotalForScience + hisSchoolFees;

                            returningStudentNonePaymentStudentInfoForScience =
                                returningStudentNonePaymentStudentInfoForScience != ""
                                    ? returningStudentNonePaymentStudentInfoForScience + "," +
                                      eachStudent.StudentDetails.IdNo + "~" +
                                      eachStudent.StudentDetails.User.FullName + "~" +
                                      eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                      eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                      (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                      hisSchoolFees
                                    : eachStudent.StudentDetails.IdNo + "~" +
                                      eachStudent.StudentDetails.User.FullName + "~" +
                                      eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                      eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                      (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                      hisSchoolFees;
                            scienceRetrnClassCategory = eachStudent.StudentDetails.ClassCategory;
                        }

                        else if (classCatStatus == ClassCategoryStatus.Art)
                        {
                            returningStudentNonePaymentStudentCounterForArt =
                                returningStudentNonePaymentStudentCounterForArt + 1;
                            returningStudentNonePaymentStudentOutstandingPaymentTotalForArt =
                                returningStudentNonePaymentStudentOutstandingPaymentTotalForArt + hisSchoolFees;

                            returningStudentNonePaymentStudentInfoForArt =
                                returningStudentNonePaymentStudentInfoForArt != ""
                                    ? returningStudentNonePaymentStudentInfoForArt + "," +
                                      eachStudent.StudentDetails.IdNo + "~" +
                                      eachStudent.StudentDetails.User.FullName + "~" +
                                      eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                      eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                      (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                      hisSchoolFees
                                    : eachStudent.StudentDetails.IdNo + "~" +
                                      eachStudent.StudentDetails.User.FullName + "~" +
                                      eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                      eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                      (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                      hisSchoolFees;
                            artRetrnClassCategory = eachStudent.StudentDetails.ClassCategory;
                        }
                        else if (classCatStatus == ClassCategoryStatus.Commercial)
                        {
                            returningStudentNonePaymentStudentCounterForCommercial =
                                returningStudentNonePaymentStudentCounterForCommercial + 1;
                            returningStudentNonePaymentStudentOutstandingPaymentTotalForCommercial =
                                returningStudentNonePaymentStudentOutstandingPaymentTotalForCommercial + hisSchoolFees;

                            returningStudentNonePaymentStudentInfoForCommercial =
                                returningStudentNonePaymentStudentInfoForCommercial != ""
                                    ? returningStudentNonePaymentStudentInfoForCommercial + "," +
                                      eachStudent.StudentDetails.IdNo + "~" +
                                      eachStudent.StudentDetails.User.FullName + "~" +
                                      eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                      eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                      (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                      hisSchoolFees
                                    : eachStudent.StudentDetails.IdNo + "~" +
                                      eachStudent.StudentDetails.User.FullName + "~" +
                                      eachStudent.StudentDetails.ClassArm.Classs.ClassLabel +
                                      eachStudent.StudentDetails.ClassArm.ArmName + "?" +
                                      (byte)eachStudent.StudentDetails.ClassArm.Classs.ClassName + "~" +
                                      hisSchoolFees;
                            commercialRetrnClassCategory = eachStudent.StudentDetails.ClassCategory;
                        }
                    }
                }
            }
            if (playClassFreshClassCategory != ClassCategoryStatus.None)
            {
                studentFieldExtractionList.Add(new TermlyReportOfReturningDto
                {
                    RowStatus = Common.Shared.StudentStatus.Fresher,
                    ClassCategory = playClassFreshClassCategory,
                    Fees = (double)freshPlayClass,
                    ClearedStuds = (short)freshiersWithCompletePaymentCounterForPlayClass,
                    ClearedStudsDetails = freshiersInfoWithCompletePaymentForPlayClass,

                    Discount = playClassFreshDiscount,

                    TakeHome1 = freshiersWithCompletePaymentTotalAmountForPlayClass,
                    PartlyClearedStuds = (short)freshiersWithOutstandingPaymentCounterForPlayClass,
                    PartlyClearedStudsDetails = freshiersInfoWithOutstandingPaymentForPlayClass,

                    TakeHome2 = freshiersPartPaymentTotalAmountForPlayClass,
                    Outstanding1 = freshiersWithOutstandingPaymentTotalAmountForPlayClass,

                    UnClearStuds = (short)freshierNonePaymentStudentCounterForPlayClass,
                    UnClearStudsDetails = freshierNonePaymentStudentInfoForPlayClass,


                    Outstanding2 = (double)freshierNonePaymentStudentOutstandingPaymentTotalForPlayClass,
                    TermAndSession = termAndSession
                });
            }
            if (playClassRetrnClassCategory != ClassCategoryStatus.None)
            {
                studentFieldExtractionList.Add(new TermlyReportOfReturningDto
                {
                    RowStatus = Common.Shared.StudentStatus.Returning,
                    ClassCategory = playClassRetrnClassCategory,
                    Fees = (double)retnPlayClass,
                    ClearedStuds = (short)returningStudentsWithCompletePaymentCounterForPlayClass,
                    ClearedStudsDetails = returningStudentsInfoWithCompletePaymentForPlayClass,

                    Discount = playClassRetrnDiscount,

                    TakeHome1 = returningStudentsWithCompletePaymentTotalAmountForPlayClass,
                    PartlyClearedStuds = (short)returningStudentsWithOutstandingPaymentCounterForPlayClass,
                    PartlyClearedStudsDetails = returningStudentsInfoWithOutstandingPaymentForPlayClass,

                    TakeHome2 = returningStudentsPartPaymentTotalAmountForPlayClass,
                    Outstanding1 = returningStudentsWithOutstandingPaymentTotalAmountForPlayClass,

                    UnClearStuds = (short)returningStudentNonePaymentStudentCounterForPlayClass,
                    UnClearStudsDetails = returningStudentNonePaymentStudentInfoForPlayClass,

                    Outstanding2 = returningStudentNonePaymentStudentOutstandingPaymentTotalForPlayClass,
                    TermAndSession = termAndSession,
                });
            }
            if (preNurseryFreshClassCategory != ClassCategoryStatus.None)
            {
                studentFieldExtractionList.Add(new TermlyReportOfReturningDto
                {
                    RowStatus = Common.Shared.StudentStatus.Fresher,
                    ClassCategory = preNurseryFreshClassCategory,
                    Fees = (double)freshPreNursery,
                    ClearedStuds = (short)freshiersWithCompletePaymentCounterForPreNursery,
                    ClearedStudsDetails = freshiersInfoWithCompletePaymentForPreNursery,

                    Discount = preNurseryFreshDiscount,

                    TakeHome1 = freshiersWithCompletePaymentTotalAmountForPreNursery,
                    PartlyClearedStuds = (short)freshiersWithOutstandingPaymentCounterForPreNursery,
                    PartlyClearedStudsDetails = freshiersInfoWithOutstandingPaymentForPreNursery,

                    TakeHome2 = freshiersPartPaymentTotalAmountForPreNursery,
                    Outstanding1 = freshiersWithOutstandingPaymentTotalAmountForPreNursery,

                    UnClearStuds = (short)freshierNonePaymentStudentCounterForPreNursery,
                    UnClearStudsDetails = freshierNonePaymentStudentInfoForPreNursery,


                    Outstanding2 = (double)freshierNonePaymentStudentOutstandingPaymentTotalForPreNursery,
                    TermAndSession = termAndSession
                });
            }
            if (preNurseryRetrnClassCategory != ClassCategoryStatus.None)
            {
                studentFieldExtractionList.Add(new TermlyReportOfReturningDto
                {
                    RowStatus = Common.Shared.StudentStatus.Returning,
                    ClassCategory = preNurseryRetrnClassCategory,
                    Fees = (double)retnPreNursery,
                    ClearedStuds = (short)returningStudentsWithCompletePaymentCounterForPreNursery,
                    ClearedStudsDetails = returningStudentsInfoWithCompletePaymentForPreNursery,

                    Discount = preNurseryRetrnDiscount,

                    TakeHome1 = returningStudentsWithCompletePaymentTotalAmountForPreNursery,
                    PartlyClearedStuds = (short)returningStudentsWithOutstandingPaymentCounterForPreNursery,
                    PartlyClearedStudsDetails = returningStudentsInfoWithOutstandingPaymentForPreNursery,

                    TakeHome2 = returningStudentsPartPaymentTotalAmountForPreNursery,
                    Outstanding1 = returningStudentsWithOutstandingPaymentTotalAmountForPreNursery,

                    UnClearStuds = (short)returningStudentNonePaymentStudentCounterForPreNursery,
                    UnClearStudsDetails = returningStudentNonePaymentStudentInfoForPreNursery,

                    Outstanding2 = returningStudentNonePaymentStudentOutstandingPaymentTotalForPreNursery,
                    TermAndSession = termAndSession,
                });
            }
            if (nurseryFreshClassCategory != ClassCategoryStatus.None)
            {
                studentFieldExtractionList.Add(new TermlyReportOfReturningDto
                {
                    RowStatus = Common.Shared.StudentStatus.Fresher,
                    ClassCategory = nurseryFreshClassCategory,
                    Fees = (double)freshNursery,
                    ClearedStuds = (short)freshiersWithCompletePaymentCounteForNursery,
                    ClearedStudsDetails = freshiersInfoWithCompletePaymentForNursery,

                    Discount = nurseryFreshDiscount,

                    TakeHome1 = freshiersWithCompletePaymentTotalAmountForNursery,
                    PartlyClearedStuds = (short)freshiersWithOutstandingPaymentCounterForNursery,
                    PartlyClearedStudsDetails = freshiersInfoWithOutstandingPaymentForNursery,

                    TakeHome2 = freshiersPartPaymentTotalAmountForNursery,
                    Outstanding1 = freshiersWithOutstandingPaymentTotalAmountForNursery,
                    UnClearStuds = (short)freshierNonePaymentStudentCounterForNursery,
                    UnClearStudsDetails = freshierNonePaymentStudentInfoForNursery,

                    Outstanding2 = (double)freshierNonePaymentStudentOutstandingPaymentTotalForNursery,
                    TermAndSession = termAndSession
                });
            }

            if (nurseryRetrnClassCategory != ClassCategoryStatus.None)
            {
                studentFieldExtractionList.Add(new TermlyReportOfReturningDto
                {
                    RowStatus = Common.Shared.StudentStatus.Returning,
                    ClassCategory = nurseryRetrnClassCategory,
                    Fees = (double)retnNursery,
                    ClearedStuds = (short)returningStudentsWithCompletePaymentCounteForNursery,
                    ClearedStudsDetails = returningStudentsInfoWithCompletePaymentForNursery,

                    Discount = nurseryRetrnDiscount,

                    TakeHome1 = returningStudentsWithCompletePaymentTotalAmountForNursery,
                    PartlyClearedStuds = (short)returningStudentsWithOutstandingPaymentCounterForNursery,
                    PartlyClearedStudsDetails = returningStudentsInfoWithOutstandingPaymentForNursery,

                    TakeHome2 = returningStudentsPartPaymentTotalAmountForNursery,
                    Outstanding1 = returningStudentsWithOutstandingPaymentTotalAmountForNursery,
                    UnClearStuds = (short)returningStudentNonePaymentStudentCounterForNursery,
                    UnClearStudsDetails = returningStudentNonePaymentStudentInfoForNursery,

                    Outstanding2 = returningStudentNonePaymentStudentOutstandingPaymentTotalForNursery,
                    TermAndSession = termAndSession,
                });
            }


            if (primaryFreshClassCategory != ClassCategoryStatus.None)
            {
                studentFieldExtractionList.Add(new TermlyReportOfReturningDto
                {
                    RowStatus = Common.Shared.StudentStatus.Fresher,
                    ClassCategory = primaryFreshClassCategory,
                    Fees = (double)freshPrimary,
                    ClearedStuds = (short)freshiersWithCompletePaymentCounteForPrimary,
                    ClearedStudsDetails = freshiersInfoWithCompletePaymentForPrimary,

                    Discount = primaryFreshDiscount,

                    TakeHome1 = freshiersWithCompletePaymentTotalAmountForPrimary,
                    PartlyClearedStuds = (short)freshiersWithOutstandingPaymentCounterForPrimary,
                    PartlyClearedStudsDetails = freshiersInfoWithOutstandingPaymentForPrimary,

                    TakeHome2 = freshiersPartPaymentTotalAmountForPrimary,
                    Outstanding1 = freshiersWithOutstandingPaymentTotalAmountForPrimary,
                    UnClearStuds = (short)freshierNonePaymentStudentCounterForPrimary,
                    UnClearStudsDetails = freshierNonePaymentStudentInfoForPrimary,

                    Outstanding2 = (double)freshierNonePaymentStudentOutstandingPaymentTotalForPrimary,
                    TermAndSession = termAndSession
                });
            }

            if (primaryRetrnClassCategory != ClassCategoryStatus.None)
            {
                studentFieldExtractionList.Add(new TermlyReportOfReturningDto
                {
                    RowStatus = Common.Shared.StudentStatus.Returning,
                    ClassCategory = primaryRetrnClassCategory,
                    Fees = (double)retnPrimary,
                    ClearedStuds = (short)returningStudentsWithCompletePaymentCounteForPrimary,
                    ClearedStudsDetails = returningStudentsInfoWithCompletePaymentForPrimary,

                    Discount = primaryRetrnDiscount,

                    TakeHome1 = returningStudentsWithCompletePaymentTotalAmountForPrimary,
                    PartlyClearedStuds = (short)returningStudentsWithOutstandingPaymentCounterForPrimary,
                    PartlyClearedStudsDetails = returningStudentsInfoWithOutstandingPaymentForPrimary,

                    TakeHome2 = returningStudentssPartPaymentTotalAmountForPrimary,
                    Outstanding1 = returningStudentsWithOutstandingPaymentTotalAmountForPrimary,
                    UnClearStuds = (short)returningStudentNonePaymentStudentCounterForPrimary,
                    UnClearStudsDetails = returningStudentNonePaymentStudentInforForPrimary,

                    Outstanding2 = returningStudentNonePaymentStudentOutstandingPaymentTotalForPrimary,
                    TermAndSession = termAndSession
                });
            }


            if (jssFreshClassCategory != ClassCategoryStatus.None)
            {
                studentFieldExtractionList.Add(new TermlyReportOfReturningDto
                {
                    RowStatus = Common.Shared.StudentStatus.Fresher,
                    ClassCategory = jssFreshClassCategory,
                    Fees = (double)freshJss,
                    ClearedStuds = (short)freshiersWithCompletePaymentCounteForJss,
                    ClearedStudsDetails = freshiersInfoWithCompletePaymentForJss,


                    Discount = jssFreshDiscount,
                    TakeHome1 = freshiersWithCompletePaymentTotalAmountForJss,
                    PartlyClearedStuds = (short)freshiersWithOutstandingPaymentCounterForJss,
                    PartlyClearedStudsDetails = freshiersInfoWithOutstandingPaymentForJss,

                    TakeHome2 = freshiersPartPaymentTotalAmountForJss,
                    Outstanding1 = freshiersWithOutstandingPaymentTotalAmountForJss,
                    UnClearStuds = (short)freshierNonePaymentStudentCounterForJss,
                    UnClearStudsDetails = freshierNonePaymentStudentInfoForJss,

                    Outstanding2 = (double)freshierNonePaymentStudentOutstandingPaymentTotalForJss,
                    TermAndSession = termAndSession
                });
            }

            if (jssRetrnClassCategory != ClassCategoryStatus.None)
            {
                studentFieldExtractionList.Add(new TermlyReportOfReturningDto
                {
                    RowStatus = Common.Shared.StudentStatus.Returning,
                    ClassCategory = jssRetrnClassCategory,
                    Fees = (double)retnJss,
                    ClearedStuds = (short)returningStudentsWithCompletePaymentCounteForJss,
                    ClearedStudsDetails = returningStudentsInfoWithCompletePaymentForJss,

                    Discount = jssRetrnDiscount,

                    TakeHome1 = returningStudentsWithCompletePaymentTotalAmountForJss,
                    PartlyClearedStuds = (short)returningStudentsWithOutstandingPaymentCounterForJss,
                    PartlyClearedStudsDetails = returningStudentsInfoWithOutstandingPaymentForJss,

                    TakeHome2 = returningStudentsPartPaymentTotalAmountForJss,
                    Outstanding1 = returningStudentsWithOutstandingPaymentTotalAmountForJss,
                    UnClearStuds = (short)returningStudentNonePaymentStudentCounterForJss,
                    UnClearStudsDetails = returningStudentNonePaymentStudentInfoForJss,

                    Outstanding2 = returningStudentNonePaymentStudentOutstandingPaymentTotalForJss,
                    TermAndSession = termAndSession
                });
            }

            if (scienceFreshClassCategory != ClassCategoryStatus.None)
            {
                studentFieldExtractionList.Add(new TermlyReportOfReturningDto
                {
                    RowStatus = Common.Shared.StudentStatus.Fresher,
                    ClassCategory = scienceFreshClassCategory,
                    Fees = (double)freshScience,
                    ClearedStuds = (short)freshiersWithCompletePaymentCounteForScience,
                    ClearedStudsDetails = freshiersInfoWithCompletePaymentForScience,

                    Discount = scienceFreshDiscount,

                    TakeHome1 = freshiersWithCompletePaymentTotalAmountForScience,
                    PartlyClearedStuds = (short)freshiersWithOutstandingPaymentCounterForScience,
                    PartlyClearedStudsDetails = freshiersInfoWithOutstandingPaymentForScience,

                    TakeHome2 = freshiersPartPaymentTotalAmountForScience,
                    Outstanding1 = freshiersWithOutstandingPaymentTotalAmountForScience,
                    UnClearStuds = (short)freshierNonePaymentStudentCounterForScience,
                    UnClearStudsDetails = freshierNonePaymentStudentInfoForScience,

                    Outstanding2 = (double)freshierNonePaymentStudentOutstandingPaymentTotalForScience,
                    TermAndSession = termAndSession
                });
            }

            if (scienceRetrnClassCategory != ClassCategoryStatus.None)
            {
                studentFieldExtractionList.Add(new TermlyReportOfReturningDto
                {
                    RowStatus = Common.Shared.StudentStatus.Returning,
                    ClassCategory = scienceRetrnClassCategory,
                    Fees = (double)retnScience,
                    ClearedStuds = (short)returningStudentsWithCompletePaymentCounteForScience,
                    ClearedStudsDetails = returningStudentsInfoWithCompletePaymentForScience,

                    Discount = scienceRetrnDiscount,

                    TakeHome1 = returningStudentsWithCompletePaymentTotalAmountForScience,
                    PartlyClearedStuds = (short)returningStudentsWithOutstandingPaymentCounterForScience,
                    PartlyClearedStudsDetails = returningStudentsInfoWithOutstandingPaymentForScience,

                    TakeHome2 = returningStudentsPartPaymentTotalAmountForScience,
                    Outstanding1 = returningStudentsWithOutstandingPaymentTotalAmountForScience,
                    UnClearStuds = (short)returningStudentNonePaymentStudentCounterForScience,
                    UnClearStudsDetails = returningStudentNonePaymentStudentInfoForScience,
                    Outstanding2 = returningStudentNonePaymentStudentOutstandingPaymentTotalForScience,
                    TermAndSession = termAndSession
                });
            }

            if (artFreshClassCategory != ClassCategoryStatus.None)
            {
                studentFieldExtractionList.Add(new TermlyReportOfReturningDto
                {
                    RowStatus = Common.Shared.StudentStatus.Fresher,
                    ClassCategory = artFreshClassCategory,
                    Fees = (double)freshArt,
                    ClearedStuds = (short)freshiersWithCompletePaymentCounteForArt,
                    ClearedStudsDetails = freshiersInfoWithCompletePaymentForArt,

                    Discount = artFreshDiscount,

                    TakeHome1 = freshiersWithCompletePaymentTotalAmountForArt,
                    PartlyClearedStuds = (short)freshiersWithOutstandingPaymentCounterForArt,
                    PartlyClearedStudsDetails = freshiersInfoWithOutstandingPaymentForArt,

                    TakeHome2 = freshiersPartPaymentTotalAmountForArt,
                    Outstanding1 = freshiersWithOutstandingPaymentTotalAmountForArt,
                    UnClearStuds = (short)freshierNonePaymentStudentCounterForArt,
                    UnClearStudsDetails = freshierNonePaymentStudentInfoForArt,

                    Outstanding2 = (double)freshierNonePaymentStudentOutstandingPaymentTotalForArt,
                    TermAndSession = termAndSession
                });
            }

            if (artRetrnClassCategory != ClassCategoryStatus.None)
            {
                studentFieldExtractionList.Add(new TermlyReportOfReturningDto
                {
                    RowStatus = Common.Shared.StudentStatus.Returning,
                    ClassCategory = artRetrnClassCategory,
                    Fees = (double)retnArt,
                    ClearedStuds = (short)returningStudentsWithCompletePaymentCounteForArt,
                    ClearedStudsDetails = returningStudentsInfoWithCompletePaymentForArt,

                    Discount = artRetrnDiscount,

                    TakeHome1 = returningStudentsWithCompletePaymentTotalAmountForArt,
                    PartlyClearedStuds = (short)returningStudentsWithOutstandingPaymentCounterForArt,
                    PartlyClearedStudsDetails = returningStudentsInfoWithOutstandingPaymentForArt,

                    TakeHome2 = returningStudentsPartPaymentTotalAmountForArt,
                    Outstanding1 = returningStudentsWithOutstandingPaymentTotalAmountForArt,
                    UnClearStuds = (short)returningStudentNonePaymentStudentCounterForArt,
                    UnClearStudsDetails = returningStudentNonePaymentStudentInfoForArt,

                    Outstanding2 = returningStudentNonePaymentStudentOutstandingPaymentTotalForArt,
                    TermAndSession = termAndSession
                });
            }

            if (commercialFreshClassCategory != ClassCategoryStatus.None)
            {
                studentFieldExtractionList.Add(new TermlyReportOfReturningDto
                {
                    RowStatus = Common.Shared.StudentStatus.Fresher,
                    ClassCategory = commercialFreshClassCategory,
                    Fees = (double)freshCommercial,
                    ClearedStuds = (short)freshiersWithCompletePaymentCounteForCommercial,
                    ClearedStudsDetails = freshiersInfoWithCompletePaymentForCommercial,

                    Discount = commercialFreshDiscount,

                    TakeHome1 = freshiersWithCompletePaymentTotalAmountForCommercial,
                    PartlyClearedStuds = (short)freshiersWithOutstandingPaymentCounterForCommercial,
                    PartlyClearedStudsDetails = freshiersInfoWithOutstandingPaymentForCommercial,

                    TakeHome2 = freshiersPartPaymentTotalAmountForCommercial,
                    Outstanding1 = freshiersWithOutstandingPaymentTotalAmountForCommercial,
                    UnClearStuds = (short)freshierNonePaymentStudentCounterForCommercial,
                    UnClearStudsDetails = freshierNonePaymentStudentInfoForCommercial,

                    Outstanding2 = (double)freshierNonePaymentStudentOutstandingPaymentTotalForCommercial,
                    TermAndSession = termAndSession
                });
            }

            if (commercialRetrnClassCategory != ClassCategoryStatus.None)
            {
                studentFieldExtractionList.Add(new TermlyReportOfReturningDto
                {
                    RowStatus = Common.Shared.StudentStatus.Returning,
                    ClassCategory = commercialRetrnClassCategory,
                    Fees = (double)retnCommercial,
                    ClearedStuds = (short)returningStudentsWithCompletePaymentCounteForCommercial,
                    ClearedStudsDetails = returningStudentsInfoWithCompletePaymentForCommercial,

                    Discount = commercialRetrnDiscount,

                    TakeHome1 = returningStudentsWithCompletePaymentTotalAmountForCommercial,
                    PartlyClearedStuds = (short)returningStudentsWithOutstandingPaymentCounterForCommercial,
                    PartlyClearedStudsDetails = returningStudentsInfoWithOutstandingPaymentForCommercial,

                    TakeHome2 = returningStudentsPartPaymentTotalAmountForCommercial,
                    Outstanding1 = returningStudentsWithOutstandingPaymentTotalAmountForCommercial,
                    UnClearStuds = (short)returningStudentNonePaymentStudentCounterForCommercial,
                    UnClearStudsDetails = returningStudentNonePaymentStudentInfoForCommercial,

                    Outstanding2 = returningStudentNonePaymentStudentOutstandingPaymentTotalForCommercial,
                    TermAndSession = termAndSession
                });
            }
            var paymentTypeTotal = new List<PaymentTypeTotalDto>
            {
                new PaymentTypeTotalDto {PaymentType = PaymentType.Cash, TotalPayment = cashPaymentTypeTotal},
                new PaymentTypeTotalDto {PaymentType = PaymentType.Teller, TotalPayment = tellerPaymentTypeTotal},
                new PaymentTypeTotalDto {PaymentType = PaymentType.Transfer, TotalPayment = transferPaymentTypeTotal}
            };
            return new TermlyPaymentReportDto
            {
                TermlyReportOfReturning = studentFieldExtractionList,
                PaymentTypeTotal = paymentTypeTotal
            };
        }

        public void DistributeStudentEvenly()
        {
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            //var employeeModel = CurrentTeacherModel();
            //var schoolId = CurrentTeacherModel()
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var getStudent = studentRepository.Where(b => b.ClassArm.Classs.SchoolInfoId == schoolId,
                b => b.ClassArm.Classs);
            var studentAccessmentsRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment, int>>();
            var resultCollections =
                studentAccessmentsRepository.Where(a => a.Student.SchoolInfoId == schoolId, a => a.Student,
                    x => x.Subject.SubjectCategory).ToList();
            var resultCollectionsGroupByStudentId = resultCollections.GroupBy(x => x.StudentId);
            foreach (var eachStudent in resultCollectionsGroupByStudentId)
            {
                var list = eachStudent.ToList();
                foreach (var eachSubject in list)
                {
                    if (eachSubject.Subject.SubjectCategory.SubjectStatus == SubjectStatus.Science)
                    {
                        var studentDetails = eachSubject.Student;
                        studentDetails.ClassCategory = ClassCategoryStatus.Science;
                        studentRepository.Update(studentDetails);
                        break;
                    }
                    if (eachSubject.Subject.SubjectCategory.SubjectStatus == SubjectStatus.Art)
                    {
                        var studentDetails = eachSubject.Student;
                        studentDetails.ClassCategory = ClassCategoryStatus.Art;
                        studentRepository.Update(studentDetails);
                        break;
                    }

                    if (eachSubject.Subject.SubjectCategory.SubjectStatus == SubjectStatus.Commercial)
                    {
                        var studentDetails = eachSubject.Student;
                        studentDetails.ClassCategory = ClassCategoryStatus.Commercial;
                        studentRepository.Update(studentDetails);
                        break;
                    }
                }
            }
            //foreach (var item in getStudent)
            //{
            //    item.Status = Common.Shared.StudentStatus.Returning;
            //    var className = item.ClassArm.Classs.ClassName;
            //    if (className == CurrentClass.PreNursery)
            //    {
            //        item.ClassCategory = ClassCategoryStatus.PreNursery;
            //    }
            //    else if (className == CurrentClass.Nursery1 || className == CurrentClass.Nursery2)
            //    {
            //        item.ClassCategory = ClassCategoryStatus.Nursery;
            //    }
            //    else if (className == CurrentClass.Primary1 || className == CurrentClass.Primary2
            //        || className == CurrentClass.Primary3 || className == CurrentClass.Primary4
            //        || className == CurrentClass.Primary5 || className == CurrentClass.Primary6)
            //    {
            //        item.ClassCategory = ClassCategoryStatus.Primary;
            //    }
            //    else if (className == CurrentClass.Jss1 || className == CurrentClass.Jss2
            //        || className == CurrentClass.Jss3)
            //    {
            //        item.ClassCategory = ClassCategoryStatus.Jss;
            //    }
            //    studentRepository.Update(item);
            //}
            UnitOfWork.Commit();
        }

        public string GetSummaryForAccountingDashboard()
        {
            var schoolId = CurrentTeacherModel().SchoolId;
            var yearlyAndTermlyStatus = ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var currentTermId =
                yearlyAndTermlyStatus.Find(
                    x =>
                        x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current && x.Session.SchoolInfoId == schoolId)
                    ?.Id;
            if (currentTermId != null)
            {
                var summary = GetTermlyReportByTermId(currentTermId ?? 0);
                var noOfClearedStudent = summary.TermlyReportOfReturning.Sum(f => f.ClearedStuds);
                var clearedTotalSum = summary.TermlyReportOfReturning.Sum(f => f.TakeHome1);
                var noOfPartlyCleared = summary.TermlyReportOfReturning.Sum(f => f.PartlyClearedStuds);
                var takeHomeAndOutstanding = summary.TermlyReportOfReturning.Sum(f => f.TakeHome2) + " / " +
                                             summary.TermlyReportOfReturning.Sum(f => f.Outstanding1);
                var noOfUnCleared = summary.TermlyReportOfReturning.Sum(f => f.UnClearStuds);
                var outstanding = summary.TermlyReportOfReturning.Sum(f => f.Outstanding2);
                return noOfClearedStudent + "," + clearedTotalSum + "," + noOfPartlyCleared + "," +
                       takeHomeAndOutstanding + "," + noOfUnCleared + "," + outstanding;
            }
            return "";

        }

        public IList<SelectListItem> EmployeeStatus(EmployeeStatus employeeStatus)
        {
            var status = new List<SelectListItem>
            {
                new SelectListItem {Text = employeeStatus.ToString(), Value = ((int) employeeStatus).ToString()}
            };
            var enumToList = EnumsAndExtensionMethods.GetEnumToList<EmployeeStatus>();
            status.AddRange(from item in enumToList
                            where item != employeeStatus
                            select new SelectListItem { Text = item.ToString(), Value = ((int)item).ToString() });
            return status;
        }

        public int ValidationChecks(string identificationNo, ClassCategoryStatus classCategory,
            CurrentClass currentClass)
        {
            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var isIdNoAreadyExist =
                studentDetailRepository.Find(
                    s => s.IdNo.Trim().ToLower() == identificationNo.Trim().ToLower() && s.SchoolInfoId == schoolId);
            if (isIdNoAreadyExist != null)
            {
                return 1;
            }
            if (currentClass == CurrentClass.Ss1 || currentClass == CurrentClass.Ss2 || currentClass == CurrentClass.Ss3)
            {
                if (
                    !(classCategory == ClassCategoryStatus.Science || classCategory == ClassCategoryStatus.Art ||
                      classCategory == ClassCategoryStatus.Commercial))
                {
                    return 5; //Appropriate class category needs to be selected (Science or Art or Commercial)
                }
            }
            return 2;
        }

        public EmployeeDto GetAdminAccountantDetails(string scholId)
        {
            var employeeRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var scholSnoId = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>().Find(scholId).SsNo;
            var model =
                employeeRepository.Find(s => s.SchoolId == scholId && s.User.UserName == "SuperAccountant" + scholSnoId,
                    s => s.User, x => x.SchoolInfo);
            return Mapper.Map<EmployeeDto>(model);
        }

        public bool UpdateAccountantInfo(UserDto model)
        {
            var employeeRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var schoolId = model.SchoolRegId;
            var schoolSnoId =
                ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>().Find(model.SchoolRegId).SsNo.ToString();
            ApplicationUser userModel;
            TxEmployee accountantModel;
            if (Thread.CurrentPrincipal.IsInRole("Accounting")) //Current user is Super Accountant.
            {

                accountantModel =
                    employeeRepository.Find(
                        x =>
                            x.SchoolId == schoolId && x.EmployeeStatus == Common.Shared.EmployeeStatus.Accountant &&
                            x.User.UserName != "SuperAccountant" + schoolSnoId,
                        u => u.User);
                userModel = accountantModel.User;
            }
            else //Current user is Saofficer i.e. the overall system administrator.
            {
                accountantModel =
                    employeeRepository.Find(
                        x => x.SchoolId == schoolId && x.User.UserName == "SuperAccountant" + schoolSnoId,
                        u => u.User);
                userModel = accountantModel.User;
            }
            accountantModel.EmployeeStatus = Common.Shared.EmployeeStatus.Accountant;
            employeeRepository.Update(accountantModel);
            var hasher = new PasswordHasher();

            userModel.FirstName = model.FirstName;
            userModel.SurName = model.SurName;
            userModel.MiddleName = model.MiddleName;
            userModel.PasswordHash = !string.IsNullOrWhiteSpace(model.Password)
                ? hasher.HashPassword(model.Password)
                : userModel.PasswordHash;
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, int>>();
            userRepository.Update(userModel);
            return true;
        }

        public IList<SelectListItem> GetTermsEnumTypeDropdownForSelectedSession(int selectedSessionId)
        {
            var yearlyAndTermlyStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var terms = yearlyAndTermlyStatusRepository.Where(h => h.SessionId == selectedSessionId)?.ToList();
            if (terms != null && terms.Any())
            {
                return
                    terms.OrderBy(v => v.TermStatus)
                        .Select(
                            x =>
                                new SelectListItem
                                {
                                    Text = x.TermStatus.ToString(),
                                    Value = ((int)x.TermStatus).ToString()
                                })
                        .ToList();
            }
            return new List<SelectListItem>();
        }

        public SchoolSubordinateAccountantStatusDto GetSchoolAccountantStatus()
        {

            var userRole = Thread.CurrentPrincipal.IsInRole("Registry");
            var schoolId = "";
            if (userRole)
            {
                schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            }
            else
            {
                schoolId = CurrentTeacherModel().SchoolId;
            }
            var schoolSnoId =
                ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>().Find(schoolId).SsNo;

            var subordinateAccountantModel =
                ServiceLocator.Get<IGenericRepository<TxEmployee, int>>()
                    .Find(
                        x =>
                            x.SchoolId == schoolId && x.EmployeeStatus == Common.Shared.EmployeeStatus.Accountant &&
                            x.User.UserName != "SuperAccountant" + schoolSnoId);
            if (subordinateAccountantModel != null)
            {
                return new SchoolSubordinateAccountantStatusDto
                {
                    SchoolId = schoolId,
                    SubordinateAccountantStatus = true
                };
            }
            return new SchoolSubordinateAccountantStatusDto
            {
                SchoolId = schoolId,
                SubordinateAccountantStatus = false
            };
        }

        public EmployeeDto GetSubordinateAccountantDetails(string scholId)
        {
            var employeeRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            // var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var schoolSnoId = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>().Find(scholId).SsNo;
            var model =
                employeeRepository.Find(
                    s =>
                        s.SchoolId == scholId && s.EmployeeStatus == Common.Shared.EmployeeStatus.Accountant &&
                        s.User.UserName != "SuperAccountant" + schoolSnoId, s => s.User, x => x.SchoolInfo);
            return Mapper.Map<EmployeeDto>(model);
        }

        public void CorrectStudentAcademicProgressHistoryIssues2()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var currentTermId =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>()
                    .Find(
                        z =>
                            z.Session.SchoolInfoId == schoolId &&
                            z.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).Id;

            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var studentAcademicProgressHistoryRepository =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();

            //var studentCurrentAcademicProgressHistoryRepositoryList = studentAcademicProgressHistoryRepository.Where(x => x.YearlyAndTermlyStatus.TermlyAccessmentStatus ==
            //        TermlyAccessmentStatus.Current && x.StudentDetails.SchoolInfoId == schoolId).ToList();

            var allStudentList = studentDetailRepository.Where(
                x => x.SchoolInfoId == schoolId && x.Status != Common.Shared.StudentStatus.Previous, z => z.ClassArm.Classs).ToList();

            foreach (var eachStudent in allStudentList)
            {
                var currentClass = eachStudent.ClassArm.Classs.ClassName;
                ClassCategoryStatus classCat;
                if (
                    !(currentClass == CurrentClass.Ss1 || currentClass == CurrentClass.Ss2 ||
                      currentClass == CurrentClass.Ss3))
                {
                    classCat = GetClassCategoryStatus(currentClass);
                }
                else
                {
                    classCat = eachStudent.ClassCategory;
                }
                var doesItExist = studentAcademicProgressHistoryRepository.Find(x => x.YearlyAndTermlyStatusId == currentTermId
                && x.StudentId == eachStudent.Id);
                if (doesItExist == null)
                {
                    var model = new StudentAcademicTxProgressHistory
                    {
                        YearlyAndTermlyStatusId = currentTermId,
                        StudentId = eachStudent.Id,
                        ClassId = eachStudent.ClassArm.ClassId,
                        ClassCategory = classCat
                    };
                    studentAcademicProgressHistoryRepository.Insert(model);
                }
                else
                {
                    var Paulse = 0;
                }
            }
            UnitOfWork.Commit();
        }

        public void CorrectStudentAcademicProgressHistoryIssues()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var currentTermId =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>()
                    .Find(
                        z =>
                            z.Session.SchoolInfoId == schoolId &&
                            z.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).Id;

            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var studentAcademicProgressHistoryRepository =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
            var allStudentList = studentDetailRepository.Where(
                x => x.SchoolInfoId == schoolId && x.Status == Common.Shared.StudentStatus.Returning, z => z.ClassArm.Classs).ToList();

            foreach (var eachStudent in allStudentList)
            {
                var currentClass = eachStudent.ClassArm.Classs.ClassName;
                ClassCategoryStatus classCat;
                if (
                    !(currentClass == CurrentClass.Ss1 || currentClass == CurrentClass.Ss2 ||
                      currentClass == CurrentClass.Ss3))
                {
                    classCat = GetClassCategoryStatus(currentClass);
                }
                else
                {
                    classCat = eachStudent.ClassCategory;
                }
                var doesItExist = studentAcademicProgressHistoryRepository.Find(x => x.YearlyAndTermlyStatusId == currentTermId 
                && x.StudentId== eachStudent.Id);
                if (doesItExist == null)
                {
                    var model = new StudentAcademicTxProgressHistory
                    {
                        YearlyAndTermlyStatusId = currentTermId,
                        StudentId = eachStudent.Id,
                        ClassId = eachStudent.ClassArm.ClassId,
                        ClassCategory = classCat
                    };
                    studentAcademicProgressHistoryRepository.Insert(model);
                }
                else
                {
                    var Paulse = 0;
                }
            }
            UnitOfWork.Commit();
        }
        public void CorrectStudentInitialRegistrationStatus2()
        {
            var studentAcademicProgressHistoryRepository =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();

            var studentDetailsRepository =
                ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var all = studentDetailsRepository.Where(v=>v.Status!= Common.Shared.StudentStatus.Previous,
                x => x.ClassArm.Classs).ToList();
            foreach (var each in all)
            {
                //each.AdmisnTermId = 1;
                //studentDetailsRepository.Update(each);
                var isExist =
                    studentAcademicProgressHistoryRepository.Find(
                        x => x.StudentId == each.Id && x.YearlyAndTermlyStatusId == 9);
                if (isExist == null)
                {
                    var model = new StudentAcademicTxProgressHistory
                    {
                        YearlyAndTermlyStatusId = 14,
                        ClassId = each.ClassArm.ClassId,
                        StudentId = each.Id
                    };
                    studentAcademicProgressHistoryRepository.Insert(model);
                }
            }


            UnitOfWork.Commit();
        }

        public void CorrectStudentInitialRegistrationStatus()
        {
            var studentDetailsRepository =
                ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var all = studentDetailsRepository.Where(v => v.AdmisnTermId == 2);
            foreach (var each in all)
            {
                each.AdmisnTermId = 1;
                studentDetailsRepository.Update(each);
            }


            var studentAcademicProgressHistoryRepository =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
            var collections =
                studentAcademicProgressHistoryRepository.Where(x => x.YearlyAndTermlyStatusId == 2).ToList();
            foreach (var each in collections)
            {
                studentAcademicProgressHistoryRepository.Delete(each);
            }
            UnitOfWork.Commit();
        }

        public void ClearCurrentTermAcademicProgressHistoryForPreviousStudent()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var currentTermId =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>()
                    .Find(
                        z =>
                            z.Session.SchoolInfoId == schoolId &&
                            z.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).Id;

            var studentAcademicProgressHistoryRepository =
                ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();

            var listOfStudentDuringTheSelectedTerm =
                studentAcademicProgressHistoryRepository.Where(x => x.YearlyAndTermlyStatusId == currentTermId,
                    y => y.StudentDetails).ToList();

            var pastStudents =
                listOfStudentDuringTheSelectedTerm.Where(
                    b => b.StudentDetails.Status == Common.Shared.StudentStatus.Previous).ToList();
            foreach (var each in pastStudents)
            {
                studentAcademicProgressHistoryRepository.Delete(each);
            }
            UnitOfWork.Commit();
        }

        public bool DoesThisPlayClassSubjectAreadyExist(string subjectName)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExist =
                subjectRepository.Find(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        sr.SubjectCategory.SubjectStatus == SubjectStatus.PlayClass);
            return doesSubjectExist == null;
        }

        public bool PlayClassSubjectUpdateValidation(string subjectName, int id)
        {
            var subjectRepository = ServiceLocator.Get<IGenericRepository<RxSubject, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var doesSubjectExistAgainstAnotherRecord =
                subjectRepository.Find(
                    sr =>
                        sr.SubjectName.Trim().ToLower() == subjectName.Trim().ToLower() &&
                        sr.SubjectCategory.SchoolId == schoolId &&
                        sr.SubjectCategory.SubjectStatus == SubjectStatus.PlayClass);
            if (doesSubjectExistAgainstAnotherRecord != null)
            {
                if (doesSubjectExistAgainstAnotherRecord.Id == id)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        public void CorrectPlayClassExtensionProblem()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var classRepository = ServiceLocator.Get<IGenericRepository<RxClasss, short>>();
            var classList = classRepository.Where(c => c.SchoolInfoId == schoolId).ToList();
            foreach (var each in classList)
            {
                var actaulClass = Next(each.ClassName);
                each.ClassName = actaulClass;
                classRepository.Update(each);
            }

            var subjectCategoryRepository = ServiceLocator.Get<IGenericRepository<SubjectRxCategories, short>>();

            var categoryList = subjectCategoryRepository.Where(c => c.SchoolId == schoolId).ToList();

            foreach (var each in categoryList)
            {
                var actaulCat = Next(each.SubjectStatus);
                each.SubjectStatus = actaulCat;
                subjectCategoryRepository.Update(each);
            }
            var checkForPlayClass =
                subjectCategoryRepository.Find(c => c.SchoolId == schoolId && c.SubjectStatus == SubjectStatus.PlayClass);
            if (checkForPlayClass == null)
            {
                var objectId = subjectCategoryRepository.Where(x => x.SchoolId == schoolId).Max(c => c.Id) + 1;
                var catModel = new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.PlayClass,
                    SchoolId = schoolId,
                    Id = (short)objectId
                };
                subjectCategoryRepository.Insert(catModel);
            }
            UnitOfWork.Commit();

            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var students =
                studentRepository.Where(
                    x =>
                        x.SchoolInfoId == schoolId, c => c.ClassArm.Classs).ToList();
            foreach (var each in students)
            {
                var hisClass = each.ClassArm.Classs.ClassName;
                if (!(hisClass == CurrentClass.Ss1 || hisClass == CurrentClass.Ss2 || hisClass == CurrentClass.Ss3))
                {
                    var classCategory = GetClassCategoryStatus(hisClass);
                    each.ClassCategory = classCategory;
                    each.CurrentClass = hisClass;
                    studentRepository.Update(each);
                }
                else
                {
                    var classCategory = Next(each.ClassCategory);
                    each.ClassCategory = classCategory;
                    each.CurrentClass = hisClass;
                    studentRepository.Update(each);
                }
            }
            UnitOfWork.Commit();
        }

        public string DecryptCodes(string code1, string code2, string code3, string code4)
        {
            var dCode1 = FreePlugIn.Encrypt(code1, true);
            var dCode2 = FreePlugIn.Encrypt(code2, true);
            var dCode3 = FreePlugIn.Encrypt(code3, true);
            var dCode4 = FreePlugIn.Encrypt(code4, true);
            return "";

        }

        public bool HasHeRegisteredAnySchool(string userId)
        {
            var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();

            var returnResult = schoolInfoRepository.Find(x => x.SchoolRegistrationOfficer.AgentUser.UserId == userId);
            if (returnResult != null)
            {
                return true;
            }
            return false;
        }

        //public bool CorrectStudentAdmisionTermId()
        //{
        //    //var currentTermId =
        //    //   ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>().Find(x=>x.TermlyAccessmentStatus==TermlyAccessmentStatus.Current);
        //    var studentDetailsRepository =
        //        ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
        //    var returning =
        //        studentDetailsRepository.Where(
        //            x =>
        //                x.Status == Common.Shared.StudentStatus.Returning ||
        //                x.Status == Common.Shared.StudentStatus.Previous);
        //    foreach (var each in returning)
        //    {
        //        each.AdmisnTermId = 1;
        //        studentDetailsRepository.Update(each);
        //    }
        //    var fresher = studentDetailsRepository.Where(x => x.Status == Common.Shared.StudentStatus.Fresher);
        //    foreach (var each in fresher)
        //    {
        //        each.AdmisnTermId = 3;
        //        studentDetailsRepository.Update(each);
        //    }
        //    UnitOfWork.Commit();
        //    return true;
        //}

        public int? CheckRenewalOrRegistrationStatus(string schoolId)
        {
            var yearlyAndTermlyStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var id =
                yearlyAndTermlyStatusRepository.Find(
                    x =>
                        x.Session.SchoolInfoId == schoolId && x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current)
                    ?.Id;
            return id;
        }

        public bool AlterStudentStatusToPrevious(byte previousStatus, string id)
        {
            var studentDetailsRepository =
                ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var studentModel = studentDetailsRepository.Find(id);
            studentModel.Status = Common.Shared.StudentStatus.Previous;
            studentModel.DateOfDischarge = DateTime.Now;
            studentDetailsRepository.Update(studentModel);

            var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();
            var currentSubjectAssessmenttList =
                studentAccessmentRepository.Where(
                    z =>
                        z.StudentId == id &&
                        z.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current).ToList();
            foreach (var each in currentSubjectAssessmenttList)
            {
                studentAccessmentRepository.Delete(each);
            }
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var schoolAccountantStatusModel =
                ServiceLocator.Get<IGenericRepository<TxEmployee, int>>()
                    .Find(
                        x =>
                            x.SchoolId == schoolId && x.EmployeeStatus == Common.Shared.EmployeeStatus.Accountant &&
                            x.User.UserName == "SuperAccountant" + schoolId);
            if (schoolAccountantStatusModel != null)
            {
                var studentTyTermlyFeePaymentRepository =
                    ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPayment, int>>();
                var currentLevel =
                    studentTyTermlyFeePaymentRepository.Find(
                        x =>
                            x.StudentAcademicProgressHistory.StudentId == id &&
                            x.StudentAcademicProgressHistory.YearlyAndTermlyStatus.TermlyAccessmentStatus ==
                            TermlyAccessmentStatus.Current);
                if (currentLevel == null)
                {
                    //Means he has not paid the current School Fees 
                    var studentAcademicProgressHistoryRepository =
                        ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
                    var hisCurrentSchoolLevel =
                        studentAcademicProgressHistoryRepository.Find(
                            x =>
                                x.StudentId == id &&
                                x.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current);
                    studentAcademicProgressHistoryRepository.Delete(hisCurrentSchoolLevel);
                }
            }
            UnitOfWork.Commit();
            return true;
        }

        public string DeleteAllAccountants(int id)
        {
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var returnString = "";
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var employeesModel = teacherRepository.Find(t => t.Id == id, x => x.SchoolInfo);
            var schoolModel = employeesModel.SchoolInfo;
            schoolModel.SchoolAdminAccountantStatus = SchoolAdminAccountantStatus.None;

            var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            schoolInfoRepository.Update(schoolModel);
            var accountantList =
                teacherRepository.Where(
                    s =>
                        s.SchoolId == employeesModel.SchoolId &&
                        s.EmployeeStatus == Common.Shared.EmployeeStatus.Accountant, z => z.User).ToList();
            foreach (var each in accountantList)
            {
                var userModel = each.User;
                returnString = returnString != "" ? returnString + "," + each.UserId : each.UserId;
                userRepository.Delete(userModel);
                teacherRepository.Delete(each);
            }
            return returnString;
        }

        public int? CheckRenewalOrRegistrationStatus()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var id =
                yearlyAndTermlyStatusRepository.Find(
                    x =>
                        x.Session.SchoolInfoId == schoolId && x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current)
                    ?.Id;
            return id;
        }

        public string DeleteAnAccountants(int id)
        {
            var teacherRepository = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var employeesModel = teacherRepository.Find(t => t.Id == id, x => x.User);
            var userModel = employeesModel.User;
            var returnString = userModel.Id;
            teacherRepository.Delete(employeesModel);
            userRepository.Delete(userModel);
            return returnString;
        }

        public string CorrectStudentAcademicProgressHistory()
        {
            var userRole = Thread.CurrentPrincipal.IsInRole("Registry");
            var schoolId = "";
            if (userRole)
            {
                schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            }
            else
            {
                schoolId = CurrentTeacherModel().SchoolId;
            }
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var studentList = studentRepository
                .Where(x => x.SchoolInfoId == schoolId && x.Status != Common.Shared.StudentStatus.Previous,
                    z => z.ClassArm.Classs)
                .ToList();

            var yearlyAndTermlyStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();

            var yearlyAndTermlyStatusId = yearlyAndTermlyStatusRepository.Find(c =>
                c.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                c.Session.SchoolInfoId == schoolId)?.Id;

            var teacherClassArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
            if (yearlyAndTermlyStatusId == null)
            {
                var academicSessionRepository =
                    ServiceLocator.Get<IGenericRepository<TxAcademicSession, short>>();
                var hasCurrentSessionAlreadySet =
                    academicSessionRepository.Find(
                        s => s.Status == Common.Shared.TimeStatus.Current && s.SchoolInfoId == schoolId);
                // Subsequent environment preparation
                if (hasCurrentSessionAlreadySet == null)
                {
                    return "You need to set the current Academic Session to continue.";
                }

                var returnInfo = CreateNewTermForNewAssessmentIst(hasCurrentSessionAlreadySet.Id, schoolId,
                    yearlyAndTermlyStatusRepository, teacherClassArmAssignmentRepository, studentList, studentRepository);

                if (returnInfo.Message != "Success")
                {
                    returnInfo.Message = "Unable to complete the process.";
                }
                return returnInfo.Message;
            }
            //var teacherClassArmAssignment =
            //            teacherClassArmAssignmentRepository.Where(
            //                t =>
            //                    t.Employee.SchoolId == schoolId &&
            //                    t.ArmTermlyAssessmentStatus == ArmTermlyAssessmentStatus.Uncompleted,
            //                t => t.Employee.User, t => t.Arm.Classs)?.ToList();

            var yearlyAndTermlyStatusObject =
                yearlyAndTermlyStatusRepository.Where(a => a.Session.SchoolInfoId == schoolId, s => s.Session)
                    .OrderByDescending(g => g.CreatedDate)
                    .First();

            var lastTermsName = yearlyAndTermlyStatusObject.TermStatus;

            var isRecordExistInteacherClassArmAssignment =
                teacherClassArmAssignmentRepository.Where(t => t.Employee.SchoolId == schoolId).Any();
            //if (isRecordExistInteacherClassArmAssignment)
            //{
            //    if (teacherClassArmAssignment != null && teacherClassArmAssignment.Count == 0)
            //    {
            if (lastTermsName == TermStatus.Third)
            {
                return "You need to now run Promotion";
            }
            CreateNewTermForNewAssessment2nd(yearlyAndTermlyStatusObject.SessionId,
                yearlyAndTermlyStatusObject, yearlyAndTermlyStatusRepository,
                teacherClassArmAssignmentRepository, schoolId, lastTermsName, studentRepository);
            //        return "Success";
            //    }
            //}
            return "Successfully completed";
        }

        public bool SetAssessmentDeadline(DateTime deadline)
        {
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyAssessmentStatusModel =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    f => f.Session.SchoolInfoId == schoolId &&
                         (f.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                          f.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing));
            if (yearlyAndTermlyAssessmentStatusModel != null)
            {
                yearlyAndTermlyAssessmentStatusModel.AssessmtEndDate = deadline;
                yearlyAndTermlyAssessmentStatusRepository.Update(yearlyAndTermlyAssessmentStatusModel);
                return true;
            }
            return false;
        }

        public DateTime? GetAssessementDeadline()
        {
            var yearlyAndTermlyAssessmentStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();
            var userRole = Thread.CurrentPrincipal.IsInRole("Registry");
            var schoolId = "";
            if (userRole)
            {
                schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            }
            else
            {
                schoolId = CurrentTeacherModel().SchoolId;
            }
            var yearlyAndTermlyAssessmentStatusModel =
                yearlyAndTermlyAssessmentStatusRepository.Find(
                    f => f.Session.SchoolInfoId == schoolId && (
                        f.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                        f.TermlyAccessmentStatus == TermlyAccessmentStatus.ResultProccessing));

            return yearlyAndTermlyAssessmentStatusModel?.AssessmtEndDate;
        }

        public bool GenderHandler(string concatParameters)
        {

            var eachValue = concatParameters.Split(',').ToList();
            var currentclass = (CurrentClass)int.Parse(eachValue[0]);
            var armName = (ArmName)int.Parse(eachValue[2]);
            var femaleStartPoint = int.Parse(eachValue[3]);
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            //No need for school Id here.
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var armStudents =
                studentRepository.Where(
                    s =>
                        s.ClassArm.Classs.ClassName == currentclass && s.ClassArm.ArmName == armName &&
                        s.SchoolInfoId == schoolId, r => r.ClassArm.Classs, r => r.User).Select(b => b.User).ToList();
            var counter = 0;
            foreach (var item in armStudents)
            {
                counter++;
                if (counter >= femaleStartPoint)
                {
                    item.Gender = Gender.Female;
                    userRepository.Update(item);
                }
            }
            return true;
        }

        public List<StudentFieldExtraction4Accounting2ndDto> GetStudentByParameter(string searchParameter)
        {
            var schoolId = CurrentTeacherModel().SchoolId; //
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            List<StudentFieldExtraction4Accounting2ndDto> students;
            if (searchParameter.Contains("/"))
            {
                students = studentRepository.Where(
                    t =>
                        t.SchoolInfoId == schoolId && t.IdNo == searchParameter, t => t.User, x => x.ClassArm.Classs)
                    .Select(x => new StudentFieldExtraction4Accounting2ndDto
                    {
                        FullName = x.User.FullName,
                        ArmName = x.ClassArm.ArmName,
                        ClassLabel = x.ClassArm.Classs.ClassLabel,
                        CurrentClass = x.ClassArm.Classs.ClassName,
                        AdmisionNo = x.IdNo,
                        StudentLevelPaymentId = x.Id,
                        ClassCategoryStatus = x.ClassCategory,
                        StudentStatus = x.Status
                    }).ToList();
            }
            else
            {
                students = studentRepository.Where(
                    t =>
                        t.SchoolInfoId == schoolId &&
                        (t.User.UserName.Trim().ToLower().Contains(searchParameter.Trim().ToLower())
                         || t.User.FirstName.Trim().ToLower().Contains(searchParameter.Trim().ToLower())
                         || t.User.MiddleName.Trim().ToLower().Contains(searchParameter.Trim().ToLower())
                         || t.User.SurName.Trim().ToLower().Contains(searchParameter.Trim().ToLower())),
                    t => t.User, x => x.ClassArm.Classs).Select(x => new StudentFieldExtraction4Accounting2ndDto
                    {
                        FullName = x.User.FullName,
                        ArmName = x.ClassArm.ArmName,
                        ClassLabel = x.ClassArm.Classs.ClassLabel,
                        CurrentClass = x.ClassArm.Classs.ClassName,
                        AdmisionNo = x.IdNo,
                        StudentLevelPaymentId = x.Id,
                        ClassCategoryStatus = x.ClassCategory,
                        StudentStatus = x.Status
                    }).ToList();
            }
            return students;
        }

        public void CorrectStudentClassCategory()
        {
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            //var students = studentRepository.Where(x => x.SchoolInfoId == schoolId, d => d.ClassArm.Classs).ToList();
            var students =
                studentRepository.Where(
                    x =>
                        x.SchoolInfoId == schoolId, v => v.ClassArm.Classs).ToList();
            foreach (var each in students)
            {

                var hisClass = Next(each.ClassArm.Classs.ClassName);

                if (!(hisClass == CurrentClass.Ss1 || hisClass == CurrentClass.Ss2 || hisClass == CurrentClass.Ss3))
                {
                    var classCategory = GetClassCategoryStatus(hisClass);
                    each.ClassCategory = classCategory;
                    each.ClassArm.Classs.ClassName = hisClass;
                    each.CurrentClass = hisClass;
                    studentRepository.Update(each);
                }
                else
                {

                    var classCategory = Next(each.ClassCategory);
                    each.ClassCategory = classCategory;
                    each.ClassArm.Classs.ClassName = hisClass;
                    each.CurrentClass = hisClass;
                    studentRepository.Update(each);
                }
            }
            UnitOfWork.Commit();
        }

        public List<StudentAcademicProgressHistoryStrokPaymentDto> GetSelectedStudentPaymentHistoryById(string studentId)
        {
            var studentTyTermlyFeePaymentRepository =
                ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPayment, int>>();
            var schoolId = CurrentTeacherModel().SchoolId;

            var setSchoolFeesRepository = ServiceLocator.Get<IGenericRepository<SetSchoolFeesTr, int>>();
            var feePaymentDiscount =
                ServiceLocator.Get<IGenericRepository<StudentTyTermlyFixedPaymentDiscount, short>>()
                    .Where(z => z.StudentAcademicProgressHistory.StudentId == studentId)?
                    .ToList();
            var feesCollection =
                setSchoolFeesRepository.Where(s => s.SchoolId == schoolId, v => v.YearlyAndTermlyStatus.Session)
                    .ToList();

            var terminalFeesDetails = new List<StudentAcademicProgressHistoryStrokPaymentDto>();
            var paymentList =
                studentTyTermlyFeePaymentRepository.Where(
                    d =>
                        d.StudentAcademicProgressHistory.StudentId == studentId,
                    s => s.StudentAcademicProgressHistory.StudentDetails.User,
                    x => x.StudentAcademicProgressHistory.YearlyAndTermlyStatus.Session,
                    g => g.StudentAcademicProgressHistory.Classs)?.ToList();

            if (paymentList != null && paymentList.Any())
            {
                var initialFeePaymentObject =
                    paymentList.OrderBy(d => d.StudentAcademicProgressHistory.YearlyAndTermlyStatusId)
                        .First();
                var initialTermId = initialFeePaymentObject.StudentAcademicProgressHistory.YearlyAndTermlyStatusId;
                var actualFeeModel = feesCollection.Find(x => x.YearlyAndTermlyStatusId == initialTermId);
                double initialFeeActualPayment = 0;
                var studentInitialRegStatus =
                    initialFeePaymentObject.StudentAcademicProgressHistory.StudentDetails.StudentInitialRegStatus;
                var classss =
                    initialFeePaymentObject.StudentAcademicProgressHistory.Classs.ClassName;
                if (studentInitialRegStatus == StudentInitialRegistrationStatus.Fresher)
                {
                    if (!(classss == CurrentClass.Ss1 || classss == CurrentClass.Ss2 || classss == CurrentClass.Ss3))
                    {
                        var className3 =
                            GetClassCategoryStatus(
                                paymentList.First()
                                    .StudentAcademicProgressHistory.Classs.ClassName);
                        {
                            initialFeeActualPayment = GetFeesForFreshiers(actualFeeModel, className3);
                            // To avoid Mistake
                        }
                    }
                    else
                    {
                        initialFeeActualPayment = GetFeesForFreshiers(actualFeeModel,
                            paymentList.First()
                                .StudentAcademicProgressHistory.StudentDetails.ClassCategory);
                    }
                }
                else
                {
                    if (!(classss == CurrentClass.Ss1 || classss == CurrentClass.Ss2 || classss == CurrentClass.Ss3))
                    {
                        var className3 =
                            GetClassCategoryStatus(
                                paymentList.First()
                                    .StudentAcademicProgressHistory.Classs.ClassName);
                        {
                            initialFeeActualPayment = GetFeesForReturningStudent(actualFeeModel, className3);
                            // To avoid Mistake
                        }
                    }
                    else
                    {
                        initialFeeActualPayment = GetFeesForReturningStudent(actualFeeModel,
                            paymentList.First()
                                .StudentAcademicProgressHistory.StudentDetails.ClassCategory);
                    }
                }
                //check if the Registration fees has been paid partly for example more than one time
                var numberOfRegistrationPartPayment =
                    paymentList.Where(
                        c => c.StudentAcademicProgressHistory.YearlyAndTermlyStatusId == initialTermId).ToList();
                var currentAcademicProgressId = numberOfRegistrationPartPayment.First().StudentAcademicProgressHistoryId;

                //var studentTermlyFeesPaymentHistory = new List<StudentTermlyFeesPaymentHistoryDto>();

                var discount =
                    feePaymentDiscount?.Find(v => v.StudentAcademicProgressId == currentAcademicProgressId)?
                        .DiscountAmount;
                var discountValue = discount;
                terminalFeesDetails.Add(new StudentAcademicProgressHistoryStrokPaymentDto
                {
                    StudentAcademicLevelId = initialFeePaymentObject.StudentAcademicProgressHistory.StudentId,
                    TermStatus = initialFeePaymentObject.StudentAcademicProgressHistory.YearlyAndTermlyStatus.TermStatus,
                    Session =
                        initialFeePaymentObject.StudentAcademicProgressHistory.YearlyAndTermlyStatus.Session.Session,
                    ActualFees = initialFeeActualPayment,
                    Discount = discountValue ?? 0,
                    StudentTermlyFeesHistory =
                        numberOfRegistrationPartPayment.Select(z => new StudentTermlyFeesPaymentHistoryDto
                        {
                            StudentAcademicLevelId = z.StudentAcademicProgressHistoryId,
                            PaymentType = z.PaymentType,
                            PaymentDate = z.PaymentDate.ToLongDateString(),
                            AmountPaid = z.Amount
                        }).ToList()
                });
                foreach (var eachPayment in numberOfRegistrationPartPayment)
                {
                    paymentList.Remove(eachPayment); //Remove registration fee collections form the entire collection
                }
                if (paymentList.Any())
                {
                    var groupPaymentList =
                        paymentList.GroupBy(
                            g => g.StudentAcademicProgressHistory.YearlyAndTermlyStatusId);

                    foreach (var eachPayment in groupPaymentList)
                    {
                        actualFeeModel =
                            feesCollection.Find(
                                x =>
                                    x.YearlyAndTermlyStatusId ==
                                    eachPayment.First().StudentAcademicProgressHistory.YearlyAndTermlyStatusId);
                        var actualFeesPayment = 0d;
                        classss = eachPayment.First().StudentAcademicProgressHistory.Classs.ClassName;
                        if (!(classss == CurrentClass.Ss1 || classss == CurrentClass.Ss2 || classss == CurrentClass.Ss3))
                        {
                            var className3 =
                                GetClassCategoryStatus(
                                    classss);
                            {
                                actualFeesPayment = GetFeesForReturningStudent(actualFeeModel, className3);
                            }
                        }
                        else
                        {
                            actualFeesPayment = GetFeesForReturningStudent(actualFeeModel,
                                eachPayment.First()
                                    .StudentAcademicProgressHistory.StudentDetails.ClassCategory);
                        }

                        var eachPaymentToList = eachPayment.ToList();
                        discount =
                            feePaymentDiscount?.Find(v => v.StudentAcademicProgressId == currentAcademicProgressId)
                                ?.DiscountAmount ?? 0;
                        discountValue = discount;
                        terminalFeesDetails.Add(new StudentAcademicProgressHistoryStrokPaymentDto
                        {
                            StudentAcademicLevelId = eachPaymentToList.First().StudentAcademicProgressHistory.StudentId,
                            TermStatus =
                                eachPaymentToList.First()
                                    .StudentAcademicProgressHistory.YearlyAndTermlyStatus.TermStatus,
                            Session =
                                eachPaymentToList.First()
                                    .StudentAcademicProgressHistory.YearlyAndTermlyStatus.Session.Session,
                            ActualFees = actualFeesPayment,
                            Discount = discountValue ?? 0,
                            StudentTermlyFeesHistory =
                                eachPaymentToList.Select(z => new StudentTermlyFeesPaymentHistoryDto
                                {
                                    StudentAcademicLevelId = z.StudentAcademicProgressHistoryId,
                                    PaymentType = z.PaymentType,
                                    PaymentDate = z.PaymentDate.ToLongDateString(),
                                    AmountPaid = z.Amount
                                }).ToList()
                        });
                    }
                }
            }
            return terminalFeesDetails;
        }

        public List<StudentDetailsDto> GetStudentsByParameter(string searchParameter)
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer(); //
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            List<StudentDetailsDto> students;
            if (searchParameter.Contains("/"))
            {
                students = Mapper.Map<List<StudentDetailsDto>>(studentRepository.Where(
                    t =>
                        t.SchoolInfoId == schoolId && t.IdNo == searchParameter, t => t.User, x => x.ClassArm.Classs));
            }
            else
            {
                students = Mapper.Map<List<StudentDetailsDto>>(studentRepository.Where(
                    t =>
                        t.SchoolInfoId == schoolId &&
                        (t.User.UserName.Trim().ToLower().Contains(searchParameter.Trim().ToLower())
                         || t.User.FirstName.Trim().ToLower().Contains(searchParameter.Trim().ToLower())
                         || t.User.MiddleName.Trim().ToLower().Contains(searchParameter.Trim().ToLower())
                         || t.User.SurName.Trim().ToLower().Contains(searchParameter.Trim().ToLower())),
                    t => t.User, x => x.ClassArm.Classs));
            }
            return students;
        }

        public string GetSystemImplementationStatus()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer(); //
            return
                ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>()
                    .Find(
                        x =>
                            x.SchoolRegistrationOfficerId == schoolId).SystemImplementationType;
        }

        public ApplicationUser GetAdminUserOfficer(string schoolId)
        {
            return ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>()
                .Find(
                    x =>
                        x.SchoolRegistrationOfficerId == schoolId, f => f.SchoolRegistrationOfficer.User)
                .SchoolRegistrationOfficer.User;
        }

        public void DeleteUser(string userId)
        {
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            userRepository.Delete(userId);
        }




        public bool HasHeRegisteredAnySchool(short userId)
        {
            var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var returnResult = schoolInfoRepository.Find(x => x.SchoolRegistrationOfficer.AgentUserId == userId);
            if (returnResult != null)
            {
                return true;
            }
            return false;
        }

        public bool DeleteAgentUser(string userId)
        {
            var userRepository = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var agentRepository = ServiceLocator.Get<IGenericRepository<Agent, short>>();
            var returnResult = userRepository.Find(x => x.Id == userId);
            var hisInfo = agentRepository.Find(c => c.UserId == userId, n => n.User);
            if (returnResult != null)
            {
                agentRepository.Delete(hisInfo);
                userRepository.Delete(returnResult);
                UnitOfWork.Commit();
            }
            return true;
        }

        public bool AccessVerification()
        {
            var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            var exermine = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>().Find(userId).AccessStatus;
            if (exermine == AccessStatus.Active)
            {
                //true condition
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool VerifyCredential(string token, string key) //Test this
        {
            var exermine =
                ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>()
                    .Find(v => v.SmsApiToken == token && v.SmsApiKey == key);
            if (exermine != null)
            {
                return false;
            }
            return true;
        }

        public string GetSmsBalance(string token, string key)
        {
            var smsPaymentBalance =
                ServiceLocator.Get<IGenericRepository<SmsPaymentBalance, int>>()
                    .Find(v => v.SchoolSmsInfo.SmsApiToken == token && v.SchoolSmsInfo.SmsApiKey == key);
            if (
                smsPaymentBalance != null)
                return smsPaymentBalance.Amount.ToString(CultureInfo.InvariantCulture) + ":" +
                       smsPaymentBalance.PricePerSms.ToString(CultureInfo.InvariantCulture);
            return "000";
        }

        public bool SubmitSmsPurchage(SmsPurchaseDto paymentModel)
        {
            var smsPurchaseRepository = ServiceLocator.Get<IGenericRepository<SmsPurchase, int>>();
            var lastSmsPurchase =
                smsPurchaseRepository.Find(
                    c => c.SchoolSmsId == paymentModel.SchoolSmsId && c.TimeStatus == Common.Shared.TimeStatus.Current);
            if (lastSmsPurchase != null)
            {
                lastSmsPurchase.TimeStatus = Common.Shared.TimeStatus.Previous;
                smsPurchaseRepository.Update(lastSmsPurchase);
            }
            var smsPaymentBalanceRepository = ServiceLocator.Get<IGenericRepository<SmsPaymentBalance, int>>();
            var balanceModel = smsPaymentBalanceRepository.Find(x => x.SchoolSmsId == paymentModel.SchoolSmsId);
            var amount = 0f;
            if (balanceModel != null)
            {
                amount = balanceModel.Amount;
                balanceModel.Amount = amount + paymentModel.AmountPurchase;
                smsPaymentBalanceRepository.Update(balanceModel);
            }
            else
            {
                var smsBalance = new SmsPaymentBalance
                {
                    SchoolSmsId = paymentModel.SchoolSmsId,
                    Amount = amount + paymentModel.AmountPurchase,
                };
                smsPaymentBalanceRepository.Insert(smsBalance);
            }
            var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            var agentId = ServiceLocator.Get<IGenericRepository<Agent, int>>().Find(c => c.UserId == userId).Id;

            var model = new SmsPurchase
            {
                AgentUserId = agentId,
                AmountPurchase = paymentModel.AmountPurchase,
                SchoolSmsId = paymentModel.SchoolSmsId,
                PaymentDate = paymentModel.PaymentDate,
                BalB4Purchase = amount,
                TimeStatus = Common.Shared.TimeStatus.Current
            };
            smsPurchaseRepository.Insert(model);
            UnitOfWork.Commit();
            return true;
        }

        public void MigrateDefaultAgent()
        {
            var smsPaymentBalanceRepo = ServiceLocator.Get<IGenericRepository<SmsPaymentBalance, int>>();
            var smsPurchaseRepo = ServiceLocator.Get<IGenericRepository<SmsPurchase, int>>();
            var currentUser = Thread.CurrentPrincipal.Identity.GetUserId();
            var agentId = ServiceLocator.Get<IGenericRepository<Agent, int>>().Find(x => x.UserId == currentUser).Id;
            var smsPurcharseModel = new SmsPurchase
            {
                SchoolSmsId = "1",
                AmountPurchase = 10,
                AgentUserId = agentId,
                BalB4Purchase = 0,
                PaymentDate = DateTime.Now,
                TimeStatus = Common.Shared.TimeStatus.Current
            };
            smsPurchaseRepo.Insert(smsPurcharseModel);

            var paymentBalanceModel = new SmsPaymentBalance
            {
                Amount = 10,
                SchoolSmsId = "1",
                PricePerSms = 2.7f
            };
            smsPaymentBalanceRepo.Insert(paymentBalanceModel);
            UnitOfWork.Commit();
        }

        public void CreateAgent(string userId, string address)
        {
            var agentRepo = ServiceLocator.Get<IGenericRepository<Agent, int>>();
            var model = new Agent
            {
                UserId = userId,
                FullAddress = address
            };
            agentRepo.Insert(model);
            UnitOfWork.SaveChanges();
        }

        public List<SmsUsedDto> GetSmsUsageHistory(string schoolId)
        {
            return
                Mapper.Map<List<SmsUsedDto>>(
                    ServiceLocator.Get<IGenericRepository<SmsUsed, int>>()
                        .Where(s => s.SchoolSmsId == schoolId, c => c.SchoolSmsInfo.SchoolRegistrationOfficer.User)
                        .Take(10)
                        .ToList());
        }

        public List<SmsPurchaseDto> GetSmsPaymentHistory(string schoolId)
        {
            return
                Mapper.Map<List<SmsPurchaseDto>>(
                    ServiceLocator.Get<IGenericRepository<SmsPurchase, int>>()
                        .Where(s => s.SchoolSmsId == schoolId, c => c.SchoolSmsInfo.SchoolRegistrationOfficer.User)
                        .Take(10)
                        .ToList());
        }

        public SmsPurchaseDto GetLastSchoolSms(string id)
        {
            return
                Mapper.Map<SmsPurchaseDto>(
                    ServiceLocator.Get<IGenericRepository<SmsPurchase, int>>()
                        .Find(s => s.SchoolSmsId == id && s.TimeStatus == Common.Shared.TimeStatus.Current,
                            c => c.SchoolSmsInfo.SchoolRegistrationOfficer.User));
        }

        public bool UpdateLastSmsPayment(SmsPurchaseDto model)
        {
            var smsPaymentBalanceRepo = ServiceLocator.Get<IGenericRepository<SmsPaymentBalance, string>>();

            var paymentModel = smsPaymentBalanceRepo.Find(model.SchoolSmsId);
            var smsPurchasesRepo = ServiceLocator.Get<IGenericRepository<SmsPurchase, int>>();
            var lastPaymentModel =
                smsPurchasesRepo.Find(
                    s => s.SchoolSmsId == model.SchoolSmsId && s.TimeStatus == Common.Shared.TimeStatus.Current);
            var balB4Purchase = lastPaymentModel.BalB4Purchase;
            var currentBalance = balB4Purchase + model.AmountPurchase;
            paymentModel.Amount = currentBalance;
            smsPaymentBalanceRepo.Update(paymentModel);
            var mapModel = Mapper.Map<SmsPurchase>(model);
            smsPurchasesRepo.Update(mapModel);
            UnitOfWork.Commit();
            return true;
        }

        public string EncodeCredentials(string credentialEncoded)
        {
            return FreePlugIn.Encrypt(credentialEncoded, true);
        }

        public string DecodeCredentials(string credentialEncoded)
        {
            return FreePlugIn.Decrypt(credentialEncoded, true);
        }

        public string UpdateSmsCredential(string smsCredential)
        {
            //var actualValue = DecodeCredentials(smsCredential);
            var decodeToParameterList = smsCredential.Split(':').ToList();
            var currentToken = decodeToParameterList[0];
            var currentKey = decodeToParameterList[1];
            var schoolTyInfoRepo = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var schoolSmsInfoObject =
                schoolTyInfoRepo.Find(
                    v => v.SmsApiToken.Trim() == currentToken.Trim() && v.SmsApiKey.Trim() == currentKey.Trim());
            if (schoolSmsInfoObject != null)
            {
                schoolSmsInfoObject.SmsApiToken = decodeToParameterList[2];
                schoolTyInfoRepo.Update(schoolSmsInfoObject);
                UnitOfWork.SaveChanges();
                return "true";
            }
            return "000";
        }

        private string DateTimeToJson(DateTime date)
        {

            return "";
        }

        public bool VerifyCredentials(string credentialEncoded)
        {
            var credentialString = DecodeCredentials(credentialEncoded);
            var splitCredentialDecoded = credentialString.Split('~').ToList();
            var token = splitCredentialDecoded[0];
            var key = splitCredentialDecoded[1];

            var expiryDate = JsonConvert.DeserializeObject<DateTime>(splitCredentialDecoded[2]);
            var licensseRepo = ServiceLocator.Get<IGenericRepository<LicensTying, int>>();

            var todayDate = DateTime.Now;
            var schoolSmsInfoObject = licensseRepo.Find(
                v =>
                    v.SchoolSmsInfo.SmsApiToken.Trim() == token.Trim() && v.SchoolSmsInfo.SmsApiKey.Trim() == key.Trim() &&
                    v.TimeStatus == Common.Shared.TimeStatus.Current);

            if (schoolSmsInfoObject != null && schoolSmsInfoObject.ExpiryDate >= todayDate)
            {
                schoolSmsInfoObject.ExpiryDate = expiryDate;
                licensseRepo.Update(schoolSmsInfoObject);
                UnitOfWork.SaveChanges();
                return true;
            }
            return false;
        }

        public List<UnAssignedClassArmSubjectDto> GetSubjectUnAssignedTeachers()
        {
            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var allStudents =
                studentDetailRepository.Where(
                    t =>
                        (t.Status == Common.Shared.StudentStatus.Fresher ||
                         t.Status == Common.Shared.StudentStatus.Returning) && t.SchoolInfoId == schoolId,
                    r => r.ClassArm.Classs, u => u.User, g => g.SchoolInfo).ToList();

            var returnList = new List<UnAssignedClassArmSubjectDto>();

            var classStudentsinAllArm = allStudents.GroupBy(s => s.ClassArm.ClassId); //Group by classes
            foreach (var eachClass in classStudentsinAllArm)
            {
                List<SubjectDto> classSubjectCollection;
                if (eachClass.First().CurrentClass == CurrentClass.Ss1 ||
                    eachClass.First().CurrentClass == CurrentClass.Ss2
                    || eachClass.First().CurrentClass == CurrentClass.Ss3)
                {
                    //Here, eachclass represent 'SS Science' or 'SS Art' or 'SS Commercial'
                    var studentDept = eachClass.GroupBy(s => s.ClassCategory);
                    foreach (var eachDept in studentDept)
                    {
                        if (eachDept.First().ClassCategory == Common.Shared.ClassCategoryStatus.Science)
                        {
                            var groupCategoryByArm = eachDept.GroupBy(a => a.ClassArmId);
                            foreach (var each in groupCategoryByArm)
                            {
                                classSubjectCollection =
                                    SeniorClassesSubjectListCurrent(Common.Shared.ClassCategoryStatus.Science);
                                var list = UnAssignedClassArmSubjectList(each, classSubjectCollection);
                                returnList.AddRange(list);
                            }
                        }
                        else if (eachDept.First().ClassCategory == Common.Shared.ClassCategoryStatus.Commercial)
                        {
                            var groupCategoryByArm = eachDept.GroupBy(a => a.ClassArmId);
                            foreach (var each in groupCategoryByArm)
                            {
                                classSubjectCollection =
                                    SeniorClassesSubjectListCurrent(Common.Shared.ClassCategoryStatus.Commercial);
                                var list = UnAssignedClassArmSubjectList(each, classSubjectCollection);
                                returnList.AddRange(list);
                            }
                        }
                        else if (eachDept.First().ClassCategory == Common.Shared.ClassCategoryStatus.Art)
                        {
                            var groupCategoryByArm = eachDept.GroupBy(a => a.ClassArmId);
                            foreach (var each in groupCategoryByArm)
                            {
                                classSubjectCollection =
                                    SeniorClassesSubjectListCurrent(Common.Shared.ClassCategoryStatus.Art);
                                var list = UnAssignedClassArmSubjectList(each, classSubjectCollection);
                                returnList.AddRange(list);
                            }
                        }
                    }
                }
                else
                {
                    classSubjectCollection =
                        BasicClassesSubjectListCurrent(eachClass.First().ClassArm.Classs.ClassName);
                    var groupCategoryByArm = eachClass.GroupBy(a => a.ClassArmId);
                    foreach (var each in groupCategoryByArm)
                    {
                        var list = UnAssignedClassArmSubjectList(each, classSubjectCollection);
                        returnList.AddRange(list);
                    }
                }
            }
            return returnList;
        }

        public byte GetSubscriptionCategoryStatus(string credentialEncoded)
        {
            var credentialString = DecodeCredentials(credentialEncoded);
            var splitCredentialDecoded = credentialString.Split(':').ToList();
            var token = splitCredentialDecoded[0];
            var key = splitCredentialDecoded[1];

            var schoolSmsInfoObject = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>().Find(
                v =>
                    v.SmsApiToken.Trim() == token.Trim() && v.SmsApiKey.Trim() == key.Trim());
            if (schoolSmsInfoObject != null)
            {
                if (FreePlugIn.Decrypt(schoolSmsInfoObject.SystemImplementationType, true).Contains(":0"))
                {
                    return 0;
                }
                else if (FreePlugIn.Decrypt(schoolSmsInfoObject.SystemImplementationType, true).Contains(":1"))
                {
                    return 1;
                }
                else
                {
                    return 9;
                }
            }
            return 2; //Un-able to verify Credentials
        }

        public List<TeacherSubjectArmAssignmentDto> ClearUnRequiredRecord()
        {
            return
                Mapper.Map<List<TeacherSubjectArmAssignmentDto>>(
                    ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>()
                        .GetAll(b => b.Arm.Classs, x => x.Employee, v => v.Subject).ToList());
        }

        public string UnAssignAndDeleteClassTeacher(IGrouping<int, TeacherSubjectArmAssignmentDto> each, int key)
        {
            var teacherSubjectArmAssignmentRepo =
                ServiceLocator.Get<IGenericRepository<TeachTxerSubjectArmAssignment, int>>();
            var teacherClassArmAssignmentsRepo =
                ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
            var employeeRepo = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var userRepo = ServiceLocator.Get<IGenericRepository<ApplicationUser, String>>();
            var castToList = each.ToList();
            foreach (var eachAssignment in castToList)
            {
                teacherSubjectArmAssignmentRepo.Delete(eachAssignment.Id);
            }
            var userId = employeeRepo.Find(x => x.Id == key).UserId;
            var listOfAssignedClassArm = teacherClassArmAssignmentsRepo.Where(c => c.EmployeeId == key).ToList();
            foreach (var eachItem in listOfAssignedClassArm)
            {
                teacherClassArmAssignmentsRepo.Delete(eachItem);
            }
            employeeRepo.Delete(key);
            userRepo.Delete(userId);
            return userId;
        }

        public void UpdateStartTermId()
        {
            var employeeRepo = ServiceLocator.Get<IGenericRepository<TxStudentDetails, string>>();
            var list = employeeRepo.Where(x => x.Status == Common.Shared.StudentStatus.Fresher).ToList();
            foreach (var each in list)
            {
                each.AdmisnTermId = 2;
                employeeRepo.Update(each);
            }
            UnitOfWork.Commit();
        }

        public short GetSchoolSnoForSuperAdminOfficer(string schoolId)
        {
            return ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>().Find(schoolId).SsNo;
        }

        public string UpdateNewSystemInfo(string credentialEncoded)
        {
            var credentialString = DecodeCredentials(credentialEncoded);
            var splitCredentialDecoded = credentialString.Split(':').ToList();
            var token = splitCredentialDecoded[0];
            var key = splitCredentialDecoded[1];
            var schoolSmsInfoRepo = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var schoolSmsInfoObject = schoolSmsInfoRepo.Find(
                v =>
                    v.SmsApiToken.Trim() == token.Trim() && v.SmsApiKey.Trim() == key.Trim());
            var newKey = FreePlugIn.Encrypt(Guid.NewGuid().ToString(), true);
            if (schoolSmsInfoObject != null)
            {
                schoolSmsInfoObject.SmsApiToken = newKey;

                schoolSmsInfoRepo.Update(schoolSmsInfoObject);
                var encode = EncodeCredentials(newKey + ":" + newKey);
                return encode;
            }
            return "000"; //Un-able to verify Credentials
        }

        public string GetSubscriptionRenewalKey(string credentialEncoded)
        {
            var credentialString = DecodeCredentials(credentialEncoded);
            var splitCredentialDecoded = credentialString.Split(':').ToList();
            var token = splitCredentialDecoded[0];
            var key = splitCredentialDecoded[1];
            var noOfStudentsInSchool = splitCredentialDecoded[3];


            var schoolSmsInfoRepo = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var schoolSmsInfoObject = schoolSmsInfoRepo.Find(
                v =>
                    v.SmsApiToken.Trim() == token.Trim() && v.SmsApiKey.Trim() == key.Trim());

            if (schoolSmsInfoObject != null)
            {
                var featureImplType = schoolSmsInfoObject.FeatureImplementationType;
                var noOfStudent = schoolSmsInfoObject.TotalStudents;
                var licensseRepo = ServiceLocator.Get<IGenericRepository<LicensTying, int>>();
                var todayDate = DateTime.Now;
                var schoolSmsInfoObjects = licensseRepo.Find(
                    v =>
                        v.SchoolSmsInfo.SmsApiToken.Trim() == token.Trim() &&
                        v.SchoolSmsInfo.SmsApiKey.Trim() == key.Trim() &&
                        v.TimeStatus == Common.Shared.TimeStatus.Current,
                    x => x.SchoolSmsInfo.SchoolRegistrationOfficer.User);

                if (schoolSmsInfoObjects != null && schoolSmsInfoObjects.ExpiryDate >= todayDate)
                {
                    if (schoolSmsInfoObjects.SchoolSmsInfo.SchoolRegistrationOfficer.User.AccessStatus ==
                        AccessStatus.Disable)
                    {
                        return "1";
                    }
                    if (schoolSmsInfoObjects.RenewalConsumptionStatus == RenewalConsumptionStatus.HasAreadyBeenConsummed)
                    {
                        return "2:" + featureImplType + ":" + noOfStudent;
                    }
                    if (schoolSmsInfoObjects.RenewalConsumptionStatus == RenewalConsumptionStatus.IsYetToBeConsummed)
                    {
                        var systemId = FreePlugIn.Decrypt(schoolSmsInfoObject.SmsApiKey, true) + ":" + noOfStudentsInSchool;
                        schoolSmsInfoObject.TotalStudents = FreePlugIn.Encrypt(systemId, true);
                        schoolSmsInfoRepo.Update(schoolSmsInfoObject);

                        var paymentDate = new JavaScriptSerializer().Serialize(schoolSmsInfoObjects.PaymentDate);
                        var expiryDate = new JavaScriptSerializer().Serialize(schoolSmsInfoObjects.ExpiryDate);
                        var returString = paymentDate + "~" + expiryDate + "~" + schoolSmsInfoObjects.SchoolSmsId + "~" +
                                          schoolSmsInfoObjects.SubscriptionId;

                        schoolSmsInfoObjects.RenewalConsumptionStatus = RenewalConsumptionStatus.HasAreadyBeenConsummed;
                        licensseRepo.Update(schoolSmsInfoObjects);
                        var encryptDate = FreePlugIn.Encrypt(returString, true);
                        var newCredential = UpdateNewSystemInfo(credentialEncoded);
                        UnitOfWork.Commit();
                        return encryptDate + "|" + newCredential + "|" + featureImplType;
                    }

                }
                return ""; //Your subscription has expired
            }
            return "000"; //Un-able to verify Credentials
        }

        public string UpdateSmsBalance(string smsCredential)
        {
            var decodeParameters = FreePlugIn.Decrypt(smsCredential, true);
            var decodeToParameterList = decodeParameters.Split(':').ToList();
            var currentToken = decodeToParameterList[0];
            var currentKey = decodeToParameterList[1];
            var smsPaymentBalanceRepo = ServiceLocator.Get<IGenericRepository<SmsPaymentBalance, int>>();
            var schoolSmsInfoObject =
                smsPaymentBalanceRepo.Find(
                    v =>
                        v.SchoolSmsInfo.SmsApiToken.Trim() == currentToken.Trim() &&
                        v.SchoolSmsInfo.SmsApiKey.Trim() == currentKey.Trim());
            if (schoolSmsInfoObject != null)
            {
                var initialBalance = schoolSmsInfoObject.Amount;
                schoolSmsInfoObject.Amount = float.Parse(decodeToParameterList[2]);
                var smsUsedModel = new SmsUsed
                {
                    SchoolSmsId = schoolSmsInfoObject.SchoolSmsId,
                    NoOfRecipient = short.Parse(decodeToParameterList[3]),
                    PricePerSms = schoolSmsInfoObject.PricePerSms,
                    NoOfPages = byte.Parse(decodeToParameterList[4]),
                    UsedDate = DateTime.Now,
                    BalB4SendindSms = initialBalance
                };
                var smsUsedRepo = ServiceLocator.Get<IGenericRepository<SmsUsed, int>>();
                smsUsedRepo.Insert(smsUsedModel);
                smsPaymentBalanceRepo.Update(schoolSmsInfoObject);
                UnitOfWork.SaveChanges();
                return "true";
            }
            return "000";
        }

        public SmsPaymentBalanceDto GetSmsPrice(string schoolId)
        {
            return
                Mapper.Map<SmsPaymentBalanceDto>(
                    ServiceLocator.Get<IGenericRepository<SmsPaymentBalance, int>>()
                        .Find(x => x.SchoolSmsId == schoolId));
        }

        public bool UpdateSmsPrice(SmsPaymentBalanceDto model)
        {
            var smsPaymentBalanceRepo = ServiceLocator.Get<IGenericRepository<SmsPaymentBalance, int>>();
            var mapModel = Mapper.Map<SmsPaymentBalance>(model);
            smsPaymentBalanceRepo.Update(mapModel);
            UnitOfWork.SaveChanges();
            return true;
        }

        public string UpdateSmsBalanceExpress(string parameterEncoded)
        {
            var decodeParameters = FreePlugIn.Decrypt(parameterEncoded, true);
            var decodeToParameterList = decodeParameters.Split(':').ToList();
            var currentToken = decodeToParameterList[0];
            var currentKey = decodeToParameterList[1];
            var noOfRecipient = float.Parse(decodeToParameterList[2]);
            var noOfPages = float.Parse(decodeToParameterList[3]);
            var smsPaymentBalanceRepo = ServiceLocator.Get<IGenericRepository<SmsPaymentBalance, int>>();
            var schoolSmsInfoObject =
                smsPaymentBalanceRepo.Find(
                    v =>
                        v.SchoolSmsInfo.SmsApiToken.Trim() == currentToken.Trim() &&
                        v.SchoolSmsInfo.SmsApiKey.Trim() == currentKey.Trim());
            if (schoolSmsInfoObject != null)
            {
                var pricePerSms = schoolSmsInfoObject.PricePerSms;
                var totalSmsCharge = noOfRecipient * noOfPages * pricePerSms;
                if (totalSmsCharge > schoolSmsInfoObject.Amount)
                {
                    return "00"; //Insufficient balance
                }
                var smsBalance = schoolSmsInfoObject.Amount - totalSmsCharge;
                schoolSmsInfoObject.Amount = smsBalance;
                var smsUsedModel = new SmsUsed
                {
                    SchoolSmsId = schoolSmsInfoObject.SchoolSmsId,
                    NoOfRecipient = (short)noOfRecipient,
                    PricePerSms = schoolSmsInfoObject.PricePerSms,
                    NoOfPages = (byte)noOfPages,
                    UsedDate = DateTime.Now,
                    BalB4SendindSms = schoolSmsInfoObject.Amount
                };
                var smsUsedRepo = ServiceLocator.Get<IGenericRepository<SmsUsed, int>>();
                smsUsedRepo.Insert(smsUsedModel);
                smsPaymentBalanceRepo.Update(schoolSmsInfoObject);
                UnitOfWork.SaveChanges();
                return "true";
            }
            return "000";
        }

        public AgentInfoDto GetAgentInfoById(string id)
        {
            var agentRepo = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var model = agentRepo.Find(id);
            if (model != null)
            {
                var returmModel = new AgentInfoDto
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    UserId = model.Id,
                    PhoneNumber = model.PhoneNumber,
                    FirstName = model.FirstName,
                    SurName = model.SurName,
                    MiddleName = model.MiddleName,
                    Gender = model.Gender,
                    //TimeStatus = model.TimeStatus,
                    AccessStatus = model.AccessStatus,
                };
                return returmModel;
            }
            return new AgentInfoDto();
        }

        public int UpdateAgentInfo(AgentInfoDto model)
        {
            var agentRepo = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();

            var hasNewEmailExistAgainstAnotherUser = agentRepo.Find(s => s.Email == model.Email)?.Id;
            if (hasNewEmailExistAgainstAnotherUser != null && hasNewEmailExistAgainstAnotherUser != model.UserId)
            {
                //Failed! Email exist agaist another record. But in this case we have to update teacher info with duplicate email.
                //Thefore we need to Proceed
                return 3;
            }
            var hasNewUsernameExistAgainstAnotherUser =
                agentRepo.Find(s => s.UserName == model.UserName)?.Id;
            if (hasNewUsernameExistAgainstAnotherUser != null &&
                hasNewUsernameExistAgainstAnotherUser != model.UserId)
            {
                return 4; //Failed! Username exist agaist another user.
            }

            var user = agentRepo.Find(model.UserId);
            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                var hasher = new PasswordHasher();
                user.PasswordHash = hasher.HashPassword(model.Password);
            }
            user.UserName = model.UserName;
            user.PhoneNumber = model.PhoneNumber;
            user.SurName = model.SurName;
            user.FirstName = model.FirstName;
            user.MiddleName = model.MiddleName;
            user.Email = model.Email;
            agentRepo.Update(user);
            UnitOfWork.SaveChanges();
            return 0;
        }

        public SchoolSmsInfoDto GetSchoolInfoById(string id)
        {
            var schoolInfoRepo = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var smsPaymentBalanceRepo = ServiceLocator.Get<IGenericRepository<SmsPaymentBalance, int>>();
            var parseId = id;
            var model = schoolInfoRepo.Find(v => v.SchoolRegistrationOfficerId == parseId,
                x => x.SchoolRegistrationOfficer.User, z => z.SchoolRegistrationOfficer.AgentUser);
            var balModel = smsPaymentBalanceRepo.Find(x => x.SchoolSmsId == model.SchoolRegistrationOfficerId);
            var smsBal = balModel?.Amount ?? 0;
            if (model != null)
            {
                var key = "";
                try
                {
                    key = FreePlugIn.Decrypt(model.SmsApiKey, true);
                }
                catch (Exception)
                {
                    key = model.SmsApiKey;
                }

                var token = "";
                try
                {
                    token = FreePlugIn.Decrypt(model.SmsApiToken, true);
                }
                catch (Exception)
                {
                    token = model.SmsApiToken;
                }
                var returmModel = new SchoolSmsInfoDto
                {
                    UserName = model.SchoolRegistrationOfficer.User.UserName,
                    Email = model.SchoolRegistrationOfficer.User.Email,
                    Id = model.SchoolRegistrationOfficerId,
                    PhoneNumber = model.SchoolRegistrationOfficer.User.PhoneNumber,
                    Motto = model.Motto,
                    SchoolAddress = model.SchoolAddress,
                    SchoolLogoString = model.SchoolLogoString,
                    SchoolName = model.SchoolName,
                    SchoolUserId = model.SchoolRegistrationOfficer.UserId,

                    SmsAutKey = key,
                    SmsAutToken = token,
                    Website = model.Website,
                    AgentUserId = model.SchoolRegistrationOfficer.AgentUserId,
                    AccessStatus = model.SchoolRegistrationOfficer.User.AccessStatus,
                    SmsBalance = smsBal,
                    SchoolAdminAccountantStatus = model.SchoolAdminAccountantStatus,
                    TotalStudents = model.TotalStudents,
                    FeatureImplementationType = model.FeatureImplementationType
                };
                return returmModel;
            }
            return new SchoolSmsInfoDto();
        }

        public string GetLastSubscriptionInfo(string schoolId)
        {
            var licensseRepo = ServiceLocator.Get<IGenericRepository<LicensTying, int>>();
            var lastSubs =
                licensseRepo.Find(x => x.SchoolSmsId == schoolId && x.TimeStatus == Common.Shared.TimeStatus.Current);
            var subscriptionId = lastSubs?.SubscriptionId.ToString();
            if (lastSubs != null)
            {
                var expiryDate = lastSubs.ExpiryDate;
                var registrationKey =
                    FreePlugIn.InternalRegistrationKeyEncodingAndExpiryDate(subscriptionId).Split(',').ToList()[0];
                return subscriptionId + "," + registrationKey + "," + expiryDate.ToLongDateString();
            }
            return null;
        }

        public List<LicensingDto> GetSubscriptionHistory(string schoolId)
        {
            return
                Mapper.Map<List<LicensingDto>>(
                    ServiceLocator.Get<IGenericRepository<LicensTying, int>>()
                        .Where(s => s.SchoolSmsId == schoolId, c => c.SchoolSmsInfo.SchoolRegistrationOfficer.User)
                        .Take(7)
                        .ToList());
        }

        public string GetExpiryDate(string id)
        {
            return
                ServiceLocator.Get<IGenericRepository<LicensTying, int>>()
                    .Find(s => s.SchoolSmsId == id && s.TimeStatus == Common.Shared.TimeStatus.Current)
                    ?.ExpiryDate.ToLongDateString();
        }

        public void DeserialiseTheFile(AllStudentsAndTeachesDto model)
        {
            var user = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>();
            var mployeeRepo = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>();
            var studentRepo = ServiceLocator.Get<IGenericRepository<TxStudentDetails, string>>();
            var classRepo = ServiceLocator.Get<IGenericRepository<RxClasss, int>>();
            var armRepo = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();

            // var userList = new List<ApplicationUser>();
            var employeeList = Mapper.Map<List<TxEmployee>>(model.Employees);
            var studentUserModels = Mapper.Map<List<TxStudentDetails>>(model.StudentDetails);
            var armModels =
                Mapper.Map<List<AxClassArm>>(
                    model.StudentDetails.Select(x => x.ClassArm).GroupBy(x => x.Id).Select(f => f.First()));
            var classsModels =
                Mapper.Map<List<RxClasss>>(
                    model.StudentDetails.Select(x => x.ClassArm.Classs).GroupBy(x => x.Id).Select(f => f.First()));

            //userList.AddRange(Mapper.Map<List<ApplicationUser>>(model.StudentDetails.Select(x => x.User)));
            //userList.AddRange(Mapper.Map<List<ApplicationUser>>(model.Employees.Select(x => x.User)));
            //var makeUnique = userList.Distinct().ToList();
            //foreach (var each in makeUnique)
            //{
            //    user.Insert(each);
            //}
            //UnitOfWork.Commit();
            //foreach (var each in employeeList)
            //{
            //    each.SchoolId = each.SchoolId.ToString();
            //    mployeeRepo.Insert(each);
            //    UnitOfWork.SaveChanges();
            //}
            //UnitOfWork.Commit();
            //foreach (var each in classsModels)
            //{
            //    classRepo.Insert(each);
            //}
            //UnitOfWork.Commit();
            //foreach (var each in armModels)
            //{
            //    armRepo.Insert(each);
            //}
            //UnitOfWork.Commit();
            var counter = 0;
            var uniquer = studentUserModels.GroupBy(s => s.User.Id).Select(c => c.First()).ToList();
            var unique3 = uniquer.Where(b => int.Parse(b.Id) <= 100);
            foreach (var each in uniquer)
            {
                var stud = studentRepo.Find(x => x.Id == each.Id);
                if (stud == null)
                {
                    counter = counter + 1;
                    studentRepo.Insert(each);
                }
            }

            UnitOfWork.Commit();
        }

        public bool GetNoOfRegisteredSchool()
        {
            var status = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>().Count();
            if (status >= 1)
            {
                return false;
            }
            return true;
        }

        //public AgentDto GetAgentInfoBySchoolId(string schoolId)
        //{
        //    throw new NotImplementedException();
        //}

        public bool CreateSchoolDefaultByAgent(string schoolName, string registrationOfficerId, string token, string key)
        {
            var schoolSubjectCategoryRepository = ServiceLocator.Get<IGenericRepository<SubjectRxCategories, short>>();
            var schoolInfoRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var schoolModel = ServiceLocator.Get<SchoolTyInfo>();
            var lastRecord = schoolInfoRepository.GetAll()?.ToList();
            var lastSerialNo = 0;
            if (lastRecord != null && lastRecord.Any())
            {
                lastSerialNo = lastRecord.Max(s => s.SsNo);
            }
            schoolModel.SchoolName = schoolName;
            lastSerialNo = (byte)(lastSerialNo + 1);
            schoolModel.SsNo = (byte)lastSerialNo;
            schoolModel.SchoolRegistrationOfficerId = registrationOfficerId;
            schoolModel.SchoolName = schoolName;
            schoolModel.SchoolAdminAccountantStatus = SchoolAdminAccountantStatus.None;
            schoolModel.SmsApiToken = token;
            schoolModel.SmsApiKey = key;
            schoolInfoRepository.Insert(schoolModel);
            UnitOfWork.SaveChanges();

            var smsPaymentBalanceRepo = ServiceLocator.Get<IGenericRepository<SmsPaymentBalance, int>>();
            var smsPurchaseRepo = ServiceLocator.Get<IGenericRepository<SmsPurchase, int>>();
            var currentUser = Thread.CurrentPrincipal.Identity.GetUserId();
            var agentId = ServiceLocator.Get<IGenericRepository<Agent, short>>().Find(x => x.UserId == currentUser).Id;
            var smsPurcharseModel = new SmsPurchase
            {
                SchoolSmsId = registrationOfficerId,
                AmountPurchase = 10,
                AgentUserId = agentId,
                BalB4Purchase = 0,
                PaymentDate = DateTime.Now,
                TimeStatus = Common.Shared.TimeStatus.Current
            };
            smsPurchaseRepo.Insert(smsPurcharseModel);

            var paymentBalanceModel = new SmsPaymentBalance
            {
                Amount = 10,
                SchoolSmsId = registrationOfficerId,
                PricePerSms = 2.7f
            };
            smsPaymentBalanceRepo.Insert(paymentBalanceModel);

            var schoolId = GetSchoolIdForProprietor();
            var newSNo = (short)(schoolSubjectCategoryRepository.Count() + 1);
            var subjectCategoryList = new List<SubjectRxCategories>
            {
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.PreNursery,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Nursery,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.General,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Primary,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Jss,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.AllSeniors,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Science,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Art,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Commercial,
                    SchoolId = schoolId,
                    Id = newSNo,
                },
                new SubjectRxCategories
                {
                    SubjectStatus = SubjectStatus.Elective,
                    SchoolId = schoolId,
                    Id = newSNo,
                }
            };
            foreach (var each in subjectCategoryList)
            {
                var getSubjectCategories = schoolSubjectCategoryRepository.GetAll();
                var sNo = schoolSubjectCategoryRepository.Count();
                var counter = 0;
                var subjectCategories = getSubjectCategories as IList<SubjectRxCategories> ??
                                        getSubjectCategories.ToList();
                if (subjectCategories.Any())
                {
                    var lastRecordId = subjectCategories.OrderBy(r => r.Id).Last().Id;
                    if (lastRecordId != sNo)
                    {
                        foreach (var eachSession in subjectCategories)
                        {
                            counter = counter + 1;
                            if (counter != eachSession.Id)
                            {
                                each.Id = (short)counter;
                                schoolSubjectCategoryRepository.Insert(each);
                                UnitOfWork.SaveChanges();
                                break;
                            }
                        }
                    }
                    else
                    {
                        sNo = sNo + 1;
                        each.Id = (short)sNo;
                        schoolSubjectCategoryRepository.Insert(each);
                        UnitOfWork.SaveChanges();
                    }
                }
                else
                {
                    sNo = sNo + 1;
                    each.Id = (short)sNo;
                    schoolSubjectCategoryRepository.Insert(each);
                    UnitOfWork.SaveChanges();
                }

            }
            return true;
        }

        public void CorrectCreatedDate()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var expensesRepository = ServiceLocator.Get<IGenericRepository<XyExpenses, int>>();
            var expensesList =
                expensesRepository.Where(f => f.Week.SchoolId == schoolId)?.ToList();

            if (expensesList != null)
            {
                foreach (var each in expensesList)
                {
                    each.CapturedDate = each.CreatedDate;
                }
            }
            UnitOfWork.Commit();
        }

        public string GetFeatureImplementationTypeForParentAndStudent()
        {
            var schoolId = CurrentStudentModel().SchoolInfoId; //
            return
                ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>()
                    .Find(v => v.SchoolRegistrationOfficerId == schoolId)
                    .FeatureImplementationType;
        }

        public string GetFeatureImplementationType()
        {
            var schoolId = CurrentTeacherModel().SchoolId; //
            return
                ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>()
                    .Find(v => v.SchoolRegistrationOfficerId == schoolId)
                    .FeatureImplementationType;
        }

        public List<OptionalChargeItemsDto> GetOptionalItemsByCategoryId(short categoryId)
        {
            var schoolId = ServiceLocator.Get<IRegistrationServise>().CurrentTeacherModel().SchoolId;
            var expensesCategoryList =
                ServiceLocator.Get<IGenericRepository<OptionalXyChargeItems, short>>()
                    .Where(x => x.ItemCategory.SchoolId == schoolId && x.ItemCategoryId == categoryId,
                        c => c.ItemCategory)
                    .ToList();
            if (expensesCategoryList.Any())
            {
                return Mapper.Map<List<OptionalChargeItemsDto>>(expensesCategoryList);
            }
            return new List<OptionalChargeItemsDto>();
        }

        public bool AssignOptionalFeeToArmStudentsOrStudentsInCategory(List<StudentIdsDto> studentIds)
        {
            var armId = studentIds.First().Id;
            var firstIndex = studentIds.Find(s => s.Id == armId);
            studentIds.Remove(firstIndex);
            var optionalFeeCategoryId = studentIds.First();
            studentIds.Remove(optionalFeeCategoryId);

            //var optionalFeeCategoryId = studentIds.First().Id;
            //firstIndex = studentIds.Find(s => s.Id == optionalFeeCategoryId);

            studentIds.Remove(firstIndex);
            var studentOPtionalFeesAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<StudentOPtionalChargesAssignment, int>>();

            var studentTermlyOptionalChargesRepository =
                ServiceLocator.Get<IGenericRepository<StudentTermlyOptionalCharges>>();

            var armIdInInt = int.Parse(armId);

            var itemCategoryId = short.Parse(optionalFeeCategoryId.Id);
            var optionalFeesItemsRepository = ServiceLocator.Get<IGenericRepository<OptionalXyChargeItems, int>>();

            var listOfOptionalFeesItemsOnTheSelectedCategory =
                optionalFeesItemsRepository.Where(c => c.ItemCategoryId == itemCategoryId).ToList();
            foreach (var eachItem in listOfOptionalFeesItemsOnTheSelectedCategory)
            {
                foreach (var each in studentIds)
                {
                    var hasItAlreadyBeenAssigned =
                        studentOPtionalFeesAssignmentRepository.Find(
                            x => x.OptionalFeesItemId == eachItem.Id && x.StudentId == each.Id);
                    if (hasItAlreadyBeenAssigned == null)
                    {
                        var model = new StudentOPtionalChargesAssignment
                        {
                            OptionalFeesItemId = eachItem.Id,
                            StudentId = each.Id,
                        };
                        studentOPtionalFeesAssignmentRepository.Insert(model);
                    }

                    var hasItAlreadyBeenAssigned2 =
                        studentTermlyOptionalChargesRepository.Find(
                            x => x.OptionalFeesItemId == eachItem.Id && x.StudentId == each.Id);
                    if (hasItAlreadyBeenAssigned2 == null)
                    {
                        var model = new StudentTermlyOptionalCharges
                        {
                            OptionalFeesItemId = eachItem.Id,
                            StudentId = each.Id,
                            ClassArmId = armIdInInt,
                            AsignedDate = DateTime.Now
                        };
                        studentTermlyOptionalChargesRepository.Insert(model);
                    }
                }
            } //

            var studentsInTheClassArm =
                studentOPtionalFeesAssignmentRepository.Where(
                    x => x.StudentDetail.ClassArmId == armIdInInt && x.OptionalFeesItem.ItemCategoryId == itemCategoryId)
                    .ToList();
            foreach (var eachItem in studentsInTheClassArm)
            {
                var doesIdExist = studentIds.Find(x => x.Id == eachItem.StudentId);
                if (doesIdExist == null)
                {
                    studentOPtionalFeesAssignmentRepository.Delete(eachItem);
                }
            }

            var studentsInTheClassArm2 =
                studentTermlyOptionalChargesRepository.Where(
                    x => x.StudentDetail.ClassArmId == armIdInInt && x.OptionalFeesItem.ItemCategoryId == itemCategoryId)
                    .ToList();
            foreach (var eachItem in studentsInTheClassArm2)
            {
                var doesIdExist = studentIds.Find(x => x.Id == eachItem.StudentId);
                if (doesIdExist == null)
                {
                    studentTermlyOptionalChargesRepository.Delete(eachItem);
                }
            }

            UnitOfWork.Commit();
            return true;
        }

        public bool UnAssignOptionalFeeToArmStudentsOrStudentsInCategory(List<StudentIdsDto> studentIds)
        {
            var armId = studentIds.First().Id;
            var firstIndex = studentIds.Find(s => s.Id == armId);
            studentIds.Remove(firstIndex);
            var categoriesId = studentIds.First().Id;
            firstIndex = studentIds.Find(s => s.Id == categoriesId);
            studentIds.Remove(firstIndex);

            var studentOPtionalFeesAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<StudentOPtionalChargesAssignment, int>>();
            var itemCategoryId = short.Parse(categoriesId);
            //var optionalFeesItemsRepository = ServiceLocator.Get<IGenericRepository<OptionalXyChargeItems, int>>();

            var armIdInInt = int.Parse(armId);
            var offeringStudentsInTheClassArm =
                studentOPtionalFeesAssignmentRepository.Where(
                    x => x.StudentDetail.ClassArmId == armIdInInt && x.OptionalFeesItem.ItemCategoryId == itemCategoryId)
                    .ToList();

            foreach (var eachItem in offeringStudentsInTheClassArm)
            {
                studentOPtionalFeesAssignmentRepository.Delete(eachItem);
            }
            var studentTermlyOptionalChargesRepository =
                ServiceLocator.Get<IGenericRepository<StudentTermlyOptionalCharges>>();
            var offeringStudentsInTheClassArm2 = studentTermlyOptionalChargesRepository.Where(
                x => x.StudentDetail.ClassArmId == armIdInInt && x.OptionalFeesItem.ItemCategoryId == itemCategoryId)
                .ToList();

            foreach (var eachItem in offeringStudentsInTheClassArm2)
            {
                studentTermlyOptionalChargesRepository.Delete(eachItem);
            }
            UnitOfWork.Commit();
            return true;
        }

        public ArmOrCategoryStudentsInTheSelectedArmDto GetStudentForAnArmForAnOptionalPayment(int armId,
            int itemCategoryId)
        {
            var subjectStudentAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<StudentOPtionalChargesAssignment, int>>();
            var studentIds =
                subjectStudentAssignmentRepository.Where(
                    s => s.StudentDetail.ClassArmId == armId && s.OptionalFeesItem.ItemCategoryId == itemCategoryId)?
                    .Select(r => r.StudentId)
                    .ToList();
            var studentDetailRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var collection =
                studentDetailRepository.Where(
                    s => s.ClassArmId == armId && s.Status != Common.Shared.StudentStatus.Previous, j => j.User);
            return new ArmOrCategoryStudentsInTheSelectedArmDto
            {
                StudentsList = Mapper.Map<List<StudentDetailsDto>>(collection),
                AssignedStudentIds = studentIds
            };
        }

        public void AutoPromotion()
        {
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            //var employeeModel = CurrentTeacherModel();
            //var schoolId = CurrentTeacherModel()

            var classArmRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
            var ss1ArmId = classArmRepository.Find(a => a.Classs.ClassName == CurrentClass.Ss1, a => a.Classs).Id;

            var collections = studentRepository.Where(b => b.ClassArm.Classs.ClassName == CurrentClass.Jss3,
                b => b.ClassArm.Classs);
            foreach (var each in collections)
            {
                each.ClassArmId = ss1ArmId;
                each.CurrentClass = CurrentClass.Ss1;
                each.ClassCategory = ClassCategoryStatus.Art;
                studentRepository.Update(each);
            }
            UnitOfWork.Commit();
        }

        public void StudentsAutomaticPromotion()
        {
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var studentList = studentRepository.Where(b => b.SchoolInfoId == schoolId && b.Status != Common.Shared.StudentStatus.Previous,
                b => b.ClassArm.Classs).ToList();

            foreach (var eachStudent in studentList)
            {
                if (eachStudent.ClassArm.Classs.ClassName == CurrentClass.Ss3)
                {
                    eachStudent.Status = Common.Shared.StudentStatus.Previous;
                    studentRepository.Update(eachStudent);
                }
                else
                {
                    var armRepository = ServiceLocator.Get<IGenericRepository<AxClassArm, int>>();
                    var nextClass = Next(eachStudent.ClassArm.Classs.ClassName);
                    var currentStudentArm = eachStudent.ClassArm.ArmName;

                    var nextClassArm =
                        armRepository.Find(
                            a =>
                                a.ArmName == currentStudentArm && a.Classs.ClassName == nextClass &&
                                a.Classs.SchoolInfoId == schoolId);
                    var execusionPoint = 0;
                    if (execusionPoint == 0 && nextClassArm == null)
                    {
                        var highestClass =
                            ServiceLocator.Get<IGenericRepository<RxClasss, int>>()
                                .Where(c => c.SchoolInfoId == schoolId)
                                .OrderBy(v => v.ClassName)
                                .Last()
                                .ClassName; //Enum.GetValues(typeof(CurrentClass)).Cast<CurrentClass>().Last();

                        while (execusionPoint == 0 && nextClassArm == null)
                        //Handle the case where classes are not serial
                        {
                            if (nextClass >= highestClass)
                            {
                                eachStudent.Status = Common.Shared.StudentStatus.Previous;
                                studentRepository.Update(eachStudent);
                                execusionPoint = 1;
                            }
                            else
                            {
                                nextClassArm = armRepository.Find(a =>
                                    a.ArmName == currentStudentArm && a.Classs.ClassName == nextClass &&
                                    a.Classs.SchoolInfoId == schoolId);

                                if (nextClassArm == null)
                                    nextClass = Next(nextClass);
                            }
                        }
                    }

                    if (execusionPoint == 1) //Signifies the final class
                    {
                        continue;
                    }
                    var nextClassArmId = nextClassArm.Id;
                    eachStudent.CurrentClass = nextClass;
                    if (nextClass == CurrentClass.Ss1)
                    {
                        eachStudent.ClassCategory = ClassCategoryStatus.Art;
                    }
                    eachStudent.ClassArmId = nextClassArmId;
                    studentRepository.Update(eachStudent);
                }
            } //
            var academicSessionRepository = ServiceLocator.Get<IGenericRepository<TxAcademicSession, short>>();

            var currentSession =
                academicSessionRepository.Find(
                    r => r.Status == Common.Shared.TimeStatus.Current && r.SchoolInfoId == schoolId);
            currentSession.Status = Common.Shared.TimeStatus.Previous;

            var yearlyAndTermlyStatusRepository =
                ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();

            var currentTermStatus =
                yearlyAndTermlyStatusRepository.Find(
                    y =>
                        y.TermlyAccessmentStatus == TermlyAccessmentStatus.Current &&
                        y.Session.SchoolInfoId == schoolId);
            currentTermStatus.TermlyAccessmentStatus = TermlyAccessmentStatus.Previous;

            academicSessionRepository.Update(currentSession);
            yearlyAndTermlyStatusRepository.Update(currentTermStatus);

            var sessionFirstComonent = int.Parse(currentSession.Session.Split('/')[0].Trim()) + 1;
            var sessionSecondComonent = int.Parse(currentSession.Session.Split('/')[1].Trim()) + 1;
            var newSession = sessionFirstComonent + "/" + sessionSecondComonent;
            var newSessionObj = new TxAcademicSession
            {
                Session = newSession,
                Status = Common.Shared.TimeStatus.Current,
                SchoolInfoId = currentSession.SchoolInfoId
            };
            academicSessionRepository.Insert(newSessionObj);

            UnitOfWork.Commit();
        }

        public IEnumerable<SchoolInfoDto> SearchSchoolByCertainParameter(string searchParameter, int searchStatus)
        {
            var schoolRegistrationRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            List<SchoolTyInfo> searchResult;
            searchParameter = searchParameter.Trim();
            var encryptSearchParameter = FreePlugIn.Encrypt(searchParameter, true);
            searchParameter = searchParameter.Trim().ToLower();
            if (searchStatus == 0)
            {
                searchResult =
                    schoolRegistrationRepository.Where(
                        x => x.SchoolRegistrationOfficer.User.UserName.Trim().ToLower().Contains(searchParameter),
                        z => z.SchoolRegistrationOfficer.User, x => x.SchoolRegistrationOfficer.AgentUser).ToList();
            }
            else if (searchStatus == 1)
            {
                searchResult =
                    schoolRegistrationRepository.Where(
                        x => x.SchoolRegistrationOfficer.User.FirstName.Trim().ToLower().Contains(searchParameter),
                        z => z.SchoolRegistrationOfficer.User, x => x.SchoolRegistrationOfficer.AgentUser).ToList();
            }
            else if (searchStatus == 2)
            {
                searchResult =
                    schoolRegistrationRepository.Where(
                        x => x.SchoolRegistrationOfficer.User.SurName.Trim().ToLower().Contains(searchParameter),
                        z => z.SchoolRegistrationOfficer.User, x => x.SchoolRegistrationOfficer.AgentUser).ToList();
            }
            else if (searchStatus == 3)
            {
                searchResult =
                    schoolRegistrationRepository.Where(
                        x =>
                            x.Telephone.Trim().Contains(searchParameter) ||
                            x.SchoolRegistrationOfficer.User.PhoneNumber.Trim().Contains(searchParameter),
                        z => z.SchoolRegistrationOfficer.User, x => x.SchoolRegistrationOfficer.AgentUser).ToList();
            }
            else if (searchStatus == 4)
            {
                searchResult =
                    schoolRegistrationRepository.Where(
                        x => x.SchoolName.Trim().ToLower().Contains(searchParameter),
                        z => z.SchoolRegistrationOfficer.User, x => x.SchoolRegistrationOfficer.AgentUser).ToList();
            }
            else if (searchStatus == 5)
            {
                searchResult =
                    schoolRegistrationRepository.Where(
                        x => x.SmsApiToken.Trim().ToLower().Contains(encryptSearchParameter),
                        z => z.SchoolRegistrationOfficer.User, x => x.SchoolRegistrationOfficer.AgentUser).ToList();
            }
            else //searchStatus == 6
            {
                searchResult =
                    schoolRegistrationRepository.Where(
                        x => x.SmsApiKey.Trim().ToLower().Contains(encryptSearchParameter),
                        z => z.SchoolRegistrationOfficer.User, x => x.SchoolRegistrationOfficer.AgentUser).ToList();
            }
            return Mapper.Map<List<SchoolInfoDto>>(searchResult);
        }

        public void ProcessAnonymousUserDefault4MonthsVailidity()
        {
            //var get4Months = DateTime.Today.AddMonths(4);
            var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            var schoolRegistrationRepository = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var schoolDetail = schoolRegistrationRepository.Find(x => x.SchoolRegistrationOfficer.UserId == userId);
            schoolDetail.MyStatus = SubscriptionStatus.HasConfirmed;
            schoolDetail.SmsApiToken = FreePlugIn.Encrypt(Guid.NewGuid().ToString(), true);

            var systemId = FreePlugIn.GetUniqueSystemId();
            var implementationType = systemId + ":0";
            schoolDetail.FeatureImplementationType = FreePlugIn.Encrypt(implementationType, true);
            schoolDetail.SmsApiKey = FreePlugIn.Encrypt(systemId, true);
            schoolRegistrationRepository.Update(schoolDetail);
        }

        public bool CorrectTeacherClassArmAssignmentIssues()
        {
            var teacherClassArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var teacherClassArmAssignmentList =
                teacherClassArmAssignmentRepository.Where(u => u.Employee.SchoolId == schoolId, u => u.Employee.User,
                    r => r.Arm.Classs).ToList();

            var affectiveAndPsychomotorAssessmentRepository =
                ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();
            var currentAffectiveAndPsychomotiveAssessmentList =
                affectiveAndPsychomotorAssessmentRepository.Where(
                    a =>
                        a.FormMaster.SchoolId == schoolId &&
                        (a.YearlyAndTermlyStatus.TermlyAccessmentStatus == TermlyAccessmentStatus.Current ||
                         a.YearlyAndTermlyStatus.TermlyAccessmentStatus ==
                         TermlyAccessmentStatus.ResultProccessing), a => a.AffectiveAndPsychomotor,
                    d => d.ClassArm.Classs,
                    c => c.FormMaster.User, v => v.Student.User, d => d.YearlyAndTermlyStatus.Session,
                    b => b.Student.ClassArm.Classs).ToList();
            var grpByClassArm = currentAffectiveAndPsychomotiveAssessmentList.GroupBy(x => x.ClassArmId);

            foreach (var each in grpByClassArm)
            {
                var eachClassArmToList = each.ToList().First();
                var appropriateTeacher =
                    teacherClassArmAssignmentList.Find(x => x.ArmId == eachClassArmToList.ClassArmId);
                if (appropriateTeacher != null)
                {
                    foreach (var eachItem in each)
                    {
                        eachItem.FormMasterId = appropriateTeacher.EmployeeId;
                        affectiveAndPsychomotorAssessmentRepository.Update(eachItem);
                    }
                }
            }
            UnitOfWork.Commit();
            return true;
        }

        public void CorrectNoOfStudentValueForAllSchool()
        {
            var schoolRepo = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var allSchools = schoolRepo.GetAll().ToList();
            foreach (var each in allSchools)
            {
                var systemIdAndNoOfStudents = "";
                try
                {
                    systemIdAndNoOfStudents = FreePlugIn.Decrypt(each.SmsApiKey, true) + ":" + each.TotalStudents ?? "0";
                }
                catch (Exception)
                {
                    systemIdAndNoOfStudents = each.SmsApiKey + ":" + each.TotalStudents ?? "0";
                }
                var encryptValue = FreePlugIn.Encrypt(systemIdAndNoOfStudents, true);
                each.TotalStudents = encryptValue;
                schoolRepo.Update(each);
            }
            UnitOfWork.Commit();
        }

        public void CorrectSystemImplementationType()
        {
            var schoolRepo = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var allSchools = schoolRepo.GetAll().ToList();
            foreach (var each in allSchools)
            {
                var systemIdAndNoOfStudents = "";
                try
                {
                    systemIdAndNoOfStudents = FreePlugIn.Decrypt(each.SmsApiKey, true) + ":" + each.SystemImplementationType ?? "0";
                }
                catch (Exception)
                {
                    systemIdAndNoOfStudents = each.SmsApiKey + ":" + each.SystemImplementationType ?? "0";
                }
                var encryptValue = FreePlugIn.Encrypt(systemIdAndNoOfStudents, true);
                each.SystemImplementationType = encryptValue;
                schoolRepo.Update(each);
            }
            UnitOfWork.Commit();
        }

        public void CorrectSmsApiKeyEncryption()
        {
            var schoolRepo = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>();
            var allSchools = schoolRepo.GetAll().ToList();
            foreach (var each in allSchools)
            {
                var systemIdAndNoOfStudents = "";
                try
                {
                    systemIdAndNoOfStudents = FreePlugIn.Decrypt(each.SmsApiKey, true);
                }
                catch (Exception)
                {
                    systemIdAndNoOfStudents = each.SmsApiKey;

                    var encryptValue = FreePlugIn.Encrypt(systemIdAndNoOfStudents, true);
                    each.SmsApiKey = encryptValue;
                    schoolRepo.Update(each);
                }

            }
            UnitOfWork.Commit();
        }

        public byte GetAppropriatePaymentStatus()
        {
            var userId = Thread.CurrentPrincipal.Identity.GetUserId();
            var schoolStudent = ServiceLocator.Get<IGenericRepository<SchoolTyInfo, string>>().Find(x => x.SchoolRegistrationOfficer.UserId == userId).TotalStudents;
            var decryptNoOfStudentInSchool = FreePlugIn.Decrypt(schoolStudent, true).Split(':').ToList()[1];

            var studentActualNo = ServiceLocator.Get<IGenericRepository<TxStudentDetails>>().Count(c => c.SchoolInfo.SchoolRegistrationOfficer.UserId == userId && c.Status != Common.Shared.StudentStatus.Previous);
            var noOfStudentInSchoolInInt = int.Parse(decryptNoOfStudentInSchool);
            int surplusNo = 0;
            if (noOfStudentInSchoolInInt >= studentActualNo)
            {

            }
            else
            {
                surplusNo = studentActualNo - noOfStudentInSchoolInInt;
            }
            return (byte)surplusNo;
        }

        public bool IsRequestToUpdateSubjectTimeStatus(int id, TimeStatus timeStatus)
        {
            var subjectTimeStatus = ServiceLocator.Get<IGenericRepository<RxSubject, int>>().Find(id).TimeStatus;
            if (subjectTimeStatus == timeStatus)
            {
                return false;
            }
            return true;
        }
        public List<string> GetUserIdsInCbtTeacherRole()
        {
            var cbtTeacherRoleId = ServiceLocator.Get<IGenericRepository<IdentityRole, string>>().Find(v => v.Name == "CbtTeacher")?.Id;

            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var userRoles = ServiceLocator.Get<IGenericRepository<TxEmployee, int>>()//CbtTeacher
                .Where(d => d.SchoolId == schoolId && d.TimeStatus==Common.Shared.TimeStatus.Current, 
                f=>f.User.Roles).Select(x=>x.User.Roles.Where(v=>v.RoleId== cbtTeacherRoleId)).ToList();
            var userIds = new List<string>();

            if (userRoles.Any())
            {
                foreach (var eachUserRole in userRoles)
                {
                    foreach (var each in eachUserRole)
                    {
                        userIds.Add(each.UserId);
                    }
                }
            }            
            return userIds;
        }

        public bool RemoveUserFromCbtRole(string userId)
        {
            var cbtTeacherRoleId = ServiceLocator.Get<IGenericRepository<IdentityRole, string>>().Find(v => v.Name == "CbtTeacher").Id;
            var user = ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>().Find(f=>f.Id== userId, x=>x.Roles);//.Roles.Remove(userRole);

            foreach (var artistType in user.Roles
                .Where(at => at.RoleId == cbtTeacherRoleId).ToList())
            {
                user.Roles.Remove(artistType);
            }
            UnitOfWork.SaveChanges();
            return true;
        }

        public bool AddUserToCbtRole(string userId)
        {
            var cbtTeacherRoleId = ServiceLocator.Get<IGenericRepository<IdentityRole, string>>().Find(v => v.Name == "CbtTeacher").Id;
            var userRole = new IdentityUserRole
            {
                RoleId = cbtTeacherRoleId,
                UserId = userId
            };
            ServiceLocator.Get<IGenericRepository<ApplicationUser, string>>()
                .Find(v=>v.Id ==userId, x=>x.Roles).Roles.Add(userRole);//CbtTeacher
            UnitOfWork.Commit();
            return true;
        }

        public void BringBackAllPreviousStudentsInThisSchool()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails, string>>();
            var allStudents = studentRepository.Where(r => r.SchoolInfoId == schoolId && r.Status == Common.Shared.StudentStatus.Previous).ToList();
            if (allStudents.Any())
            {
                foreach (var item in allStudents)
                {
                    item.Status = Common.Shared.StudentStatus.Returning;
                }
                UnitOfWork.Commit();
            }
        }

        public List<StudentDetailsDto> GetStudentsWithoutPassportByClassId(string className)
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var studentRepository = ServiceLocator.Get<IGenericRepository<TxStudentDetails, string>>();

            var armStudents =
               studentRepository.Where(
                   s => (s.Passport == null || s.Passport.Trim() == "") &&
                       s.ClassArm.Classs.ClassLabel == className &&
                       s.SchoolInfoId == schoolId && 
                       s.Status != Common.Shared.StudentStatus.Previous, r => r.ClassArm.Classs, r => r.User).ToList();
            return Mapper.Map<List<StudentDetailsDto>>(armStudents);
        }


        public string RevertToLastTerm()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var yearlyAndTermlyStatusRepository = ServiceLocator.Get<IGenericRepository<TxYearlyAndTermlyStatus, int>>();


            var currentTerm =
                   yearlyAndTermlyStatusRepository
                       .Find(
                           x =>
                               x.Session.SchoolInfoId == schoolId &&
                               x.TermlyAccessmentStatus == TermlyAccessmentStatus.Current);

            var studentAccessmentRepository = ServiceLocator.Get<IGenericRepository<StudenXtAccessment>>();

            var currentTermAssessmentList = studentAccessmentRepository
                .Where(x => x.YearlyAndTermlyStatus.Session.SchoolInfoId == schoolId &&
                x.YearlyAndTermlyStatusId == currentTerm.Id, c=> c.YearlyAndTermlyStatus).ToList();

            var hasAssessmentRecorded = currentTermAssessmentList.FirstOrDefault(x => x.Ca1 > 0 || x.Ca2 > 0 || x.Ca3 > 0);

            if (hasAssessmentRecorded != null)
            {
                return "";
            }
            if (currentTermAssessmentList.Any() && currentTermAssessmentList[0].YearlyAndTermlyStatus.TermStatus == TermStatus.First)
            {
                return "FistTerm";
            }

            foreach (var item in currentTermAssessmentList)
            {
                studentAccessmentRepository.Delete(item.Id);
            }
            
                        var affectiveAndPsychomoTxtorAssessmentRepository = ServiceLocator.Get<IGenericRepository<AffectiveAndPsychomoTxtorAssessment>>();
            var affectiveAndPsychomoTxtorAssessmentList = affectiveAndPsychomoTxtorAssessmentRepository
                    .Where(x => x.YearlyAndTermlyStatus.Session.SchoolInfoId == schoolId &&
                    x.YearlyAndTermlyStatusId == currentTerm.Id).ToList();


            foreach (var item in affectiveAndPsychomoTxtorAssessmentList)
            {
                affectiveAndPsychomoTxtorAssessmentRepository.Delete(item.Id);
            }
            var studentAcademicProgressHistoriesRepository = ServiceLocator.Get<IGenericRepository<StudentAcademicTxProgressHistory, int>>();
            var studentAcademicProgressHistoryList = studentAcademicProgressHistoriesRepository
                    .Where(x => x.YearlyAndTermlyStatus.Session.SchoolInfoId == schoolId &&
                    x.YearlyAndTermlyStatusId == currentTerm.Id).ToList();

            foreach (var item in studentAcademicProgressHistoryList)
            {
                studentAcademicProgressHistoriesRepository.Delete(item.Id);
            }
            yearlyAndTermlyStatusRepository.Delete(currentTerm.Id);
           
            UnitOfWork.Commit();
            var lastTerm = yearlyAndTermlyStatusRepository.Where(
               y => y.Session.SchoolInfoId == schoolId).OrderBy(x => x.Id).Last();
            lastTerm.TermlyAccessmentStatus = TermlyAccessmentStatus.Current;
            yearlyAndTermlyStatusRepository.Update(lastTerm);
            UnitOfWork.SaveChanges();

            return "Success";
        }
        public byte GetAllAssessmentCompletionStatus()
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
            var teacherClassArmAssignmentRepository =
               ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
            var teacherClassArmAssignmentList = teacherClassArmAssignmentRepository.Where(t =>
                    t.Employee.SchoolId == schoolId);

            var isAllCompleted = teacherClassArmAssignmentList.FirstOrDefault(x => x.ArmTermlyAssessmentStatus == ArmTermlyAssessmentStatus.Uncompleted);

            if (isAllCompleted == null)
            {
                    return (byte)ArmTermlyAssessmentStatus.Completed;//1
            }
            return (byte)ArmTermlyAssessmentStatus.Uncompleted;//0
        }

        public bool ChangeTermlyAssessmentStatus(ArmTermlyAssessmentStatus status)
        {
            var schoolId = GetSchoolIdForSchoolRegistrationOfficer();
           
            var teacherClassArmAssignmentRepository =
                ServiceLocator.Get<IGenericRepository<TeacherClassArmTxAssignment, int>>();
            var teacherClassArmAssignmentList = teacherClassArmAssignmentRepository.Where(t => 
                    t.Employee.SchoolId == schoolId).ToList();
            foreach (var item in teacherClassArmAssignmentList)
            {
                item.ArmTermlyAssessmentStatus = status;
                teacherClassArmAssignmentRepository.Update(item);
            }
            UnitOfWork.Commit();
            return true;
        }


    }
}