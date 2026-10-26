using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Naits.Common.DependencyInjection.Locator;
using Naits.Common.Shared;
using Naits.Common.Shared.Dto;
using Naits.Presentation.Web.Controllers;
using Naits.Presentation.Web.Models;
using Naits.Services.Base;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Lib;
using Naits.Presentation.Web.Infobip.Api.Client;
using Naits.Presentation.Web.Infobip.Api.Config;
using Naits.Presentation.Web.Infobip.Api.Model.Sms.Mt.Send;
using Naits.Presentation.Web.Infobip.Api.Model.Sms.Mt.Send.Textual;
using Naits.Presentation.Web.Jusibe;
using Naits.Presentation.Web.Jusibe.Models;
using static System.Convert;
using Font = iTextSharp.text.Font;
using ThreadState = System.Threading.ThreadState;
using Newtonsoft.Json;
using Naits.Model.Repositories.Base;
//using HttpClientProvider = Naits.Presentation.Web.Infobip.Api.Config.HttpClientProvider;

namespace Naits.Presentation.Web.Areas.Registry.Controllers
{
    public class RegistryLegacyController : BaseController
    {
        // GET: Registry/Registry
        private readonly IRegistrationServise _registrationService;
        //private ApplicationUserManager _userManager;
        //private readonly IUserTokenProviderService _userTokenProviderService;
        public RegistryLegacyController()
        {
            _registrationService = ServiceLocator.Get<IRegistrationServise>();
            //_contractorService = serviceLocator.Get<IContractorService>();
        }
        public async Task<ActionResult> Index()
        {
            //ServiceLocator.Get<IRegistrationServise>().CorrectStudentAcademicProgressHistoryIssues();
            
            //TheRightNaitsHostName();
            var teachersAndOthersService = ServiceLocator.Get<ITeachersAndOthersService>();
            var aStatus = teachersAndOthersService.B();//B means VerifyAccess, aStatus means checkIfUserAccessIsActive
            if (aStatus != null)
            {
                ShowMessage(aStatus.Value1, AlertType.Danger);
                return RedirectToAction(aStatus.Value2, aStatus.Value3, new { Area = aStatus.Value4 });
            }
            var securityService = ServiceLocator.Get<ISecurityService>();
            var bStatus = securityService.B();//B means VerifyAccess
            if (bStatus.AccessVerifyReturnValues != null)
            {

                ShowMessage(bStatus.AccessVerifyReturnValues.Value1, AlertType.Danger);
                return RedirectToAction(bStatus.AccessVerifyReturnValues.Value2,
                    bStatus.AccessVerifyReturnValues.Value3,
                    new { Area = bStatus.AccessVerifyReturnValues.Value4 });
            }
            if ((bStatus.LoginReturnStatus.ReturnMessage != "1") || (bStatus.LoginReturnStatus.StatusCode == "1"))
            {
                UnitOfWork.Commit();
                ShowMessage(bStatus.LoginReturnStatus.ReturnMessage, AlertType.Danger);
            }
            else
            {
                var registrationConfirmationStatus = securityService.GetSubscriptionConfirmationStatusForRegistrar();
                if (!registrationConfirmationStatus)
                {
                    var returnStatus = "";
                    if (CheckForInternetConnection())
                    {
                        var credentialEncoded =
                            ServiceLocator.Get<ITeachersAndOthersService>().GetAndEncodeCredentialsAndExpiryDate();
                        credentialEncoded = HttpUtility.UrlEncode(credentialEncoded);

                        var rightHostName = TheRightNaitsHostName();
                        if (!string.IsNullOrEmpty(rightHostName))
                        {
                            var url = rightHostName + "/home/VerifySubscription?credentialEncoded=" + credentialEncoded;
                            try
                            {
                                var balance = await SmsLib.GetSchoolSmsBalance(url);
                                if (!string.IsNullOrEmpty(balance))
                                {
                                    returnStatus = balance.Replace("\"", "");
                                    if (returnStatus == "true")
                                    {
                                        securityService.ConfirmSubscription();
                                    }
                                    else
                                    {
                                        ShowMessage("Failed! Unable to verify your credentials. Contact administrator on 08035550938.", AlertType.Warning);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                if (ex.InnerException != null) ;
                            }

                        }
                        else
                        {
                            ShowMessage("NAITS-ASMS online platform is temporarily unavailanle!!!", AlertType.Warning);
                        }
                    }
                    else
                    {
                        ShowMessage("You need to provide internet in order to confirm your registration!!!", AlertType.Warning);
                    }
                    if (returnStatus == "true")
                    {
                        ShowMessage("Good! Your Subscription has successfully been confirmed.", AlertType.Success);
                    }
                }
            }
            return View();
        }


        public ActionResult ClassReg(string clasName, int classId = 0, byte totalNoOfClasses = 0)
        {
            ViewBag.classId = classId;
            ViewBag.clasName = clasName;
            totalNoOfClasses = (byte)(totalNoOfClasses + 1);
            ViewBag.totalNoOfClasses = totalNoOfClasses;
            return View(new ClassDto { SNo = totalNoOfClasses });
        }
        [HttpPost]
        public ActionResult ClassReg(ClassDto model)
        {
            if (ModelState.IsValid)
            {
                var verifyUniques = _registrationService.IsClassAlreadyExist(model.ClassName);
                if (verifyUniques)
                {
                    if (string.IsNullOrWhiteSpace(model.ClassLabel))
                    {
                        model.ClassLabel = model.ClassName.ToString();
                    }
                    var status = _registrationService.CreateClass(model);
                    if (status)
                    {
                        UnitOfWork.Commit();
                        return RedirectToAction("ClassRegIndex");
                    }
                    else
                    {
                        ShowMessage("Sorry! You need to first register your school before creating Classes.", AlertType.Warning);
                        return RedirectToAction("SchoolRegistration");
                    }
                }
                ShowMessage($"This Class ('{model.ClassName}') already exist.", AlertType.Danger);
                return View(model);
            }
            return View(model);
        }
        public ActionResult ClassRegIndex()
        {
            //var check = _registrationService.HasSchoolBeenRegistered();
            //if (!check)
            //{
            //    ShowMessage("Sorry! You need to first register your school before continuing.", AlertType.Warning);
            //    return RedirectToAction("SchoolRegistration", "Registry", new { Area = "Registry" });
            //}
            var classCollection = _registrationService.GetClass();
            var classses = classCollection as IList<ClassDto> ?? classCollection.ToList();
            ViewBag.TotalNoOfClasses = classses.Any() ? classses.Count() : 0;
            return View(classCollection);
        }
        public ActionResult CreatClassArm(CurrentClass className, string classLabel, byte classId = 0, byte totalNoOfArms = 0, byte directional = 0)
        {
            ViewBag.clasName = className;
            ViewBag.classId = classId;
            ViewBag.classLabel = classLabel;
            ViewBag.directional = directional;
            totalNoOfArms = (byte)(totalNoOfArms + 1);
            ViewBag.totalNoOfArms = totalNoOfArms;
            var model = new ClassArmDto
            {
                ClassId = classId,
                SNo = totalNoOfArms
            };
            return View(model);
        }
        [HttpPost]
        public ActionResult CreatClassArm(ClassArmDto model, CurrentClass className, string classLabel, short classId = 0)
        {
            ViewBag.clasName = className;
            ViewBag.classLabel = classLabel;
            ViewBag.classId = classId;
            if (ModelState.IsValid)
            {
                var verifyUniques = _registrationService.IsClassArmAlreadyExist(model.ArmName, model.ClassId);
                if (verifyUniques)
                {
                    var armCollections = _registrationService.CreatClassArm(model);
                    if (armCollections)
                    {
                        UnitOfWork.Commit();
                        return RedirectToAction("ClassArmIndex", new { classLabel, className, classId });
                    }
                }
                ShowMessage($"This ClassArm ('{model.ArmName}') already exist.", AlertType.Danger);
                return View(model);
            }
            return View(model);
        }
        public ActionResult ClassArmIndex(string classLabel, CurrentClass className = CurrentClass.PreNursery, short armId = 0, short classId = 0, int directional = 0)
        {
            //The case when back button is pressed
            if (directional == 1)
            {
                return RedirectToAction("ClassRegIndex", new { directional });
            }
            ViewBag.className = className;
            ViewBag.classLabel = classLabel;
            ViewBag.classId = classId;
            ViewBag.armId = armId;

            //ModelState.Clear();
            var armCollections = _registrationService.GetClasssArm(classId);
            var classArm = armCollections as IList<ClassArmDto> ?? armCollections.ToList();
            var totalNoOfArms = classArm.Any() ? classArm.Count() : 0;
            ViewBag.totalNoOfArms = totalNoOfArms;
            if (classArm.Any())
            {
                return View(armCollections);
            }
            directional = 1;
            ShowMessage("Fresh class! Start adding Arm", AlertType.Warning);
            return RedirectToAction("CreatClassArm", new { className, classLabel, classId, totalNoOfArms, directional });
        }
        public ActionResult StudentRegIndexIst(int? classId, string classLabel, string aarmName,
            CurrentClass classs = CurrentClass.PreNursery, int armId = 0, int directional = 0)
        {
            if (string.IsNullOrEmpty(classLabel))
            {
                ViewBag.classLabel = classLabel;
                return View(new List<StudentDetailsDto>());
            }
            ViewBag.classs = classs;
            ViewBag.classLabel = classLabel;
            ViewBag.classId = classId ?? 0;
            ViewBag.aarmName = aarmName;
            ViewBag.armId = armId;
            ViewBag.directional = directional;

            if (directional == 1)
            {
                directional = 2;
                return RedirectToAction("ClassArmIndex", new { classId, classLabel, directional });
            }

            ViewBag.classId = classId;


            var studentCollections = _registrationService.GetStudentForAnArm(armId);
            var totalNoOfStudents = studentCollections.Count();
            ViewBag.totalNoOfStudents = totalNoOfStudents;
            if (studentCollections.Any())
            {
                return View(studentCollections);
            }
            directional = 1;
            ShowMessage("Fresh class! Start adding Student", AlertType.Warning);
            return RedirectToAction("CreatStudent", "Registration", new
            {
                Area = "",
                classId,
                classs,
                classLabel,
                aarmName,
                armId,
                totalNoOfStudents,
                directional
            });
        }

        public ActionResult SubjectRegIndex(string subjectLevelStatus, SubjectStatus subjectStatus, byte directional = 0)
        {
            //The case when back button is pressed
            if (directional == 1)
            {
                return RedirectToAction("SubjectIndex", new { directional });
            }
            if (string.IsNullOrEmpty(subjectLevelStatus))
            {
                ViewBag.subjectLevelStatus = subjectStatus.ToString() == "AllSeniors" ? "Senior General Subjects" : subjectStatus.ToString();
            }
            else
            {
                ViewBag.subjectLevelStatus = subjectLevelStatus;
            }

            ViewBag.subjectStatus = subjectStatus;

            var armCollections = _registrationService.GetSubjects(subjectStatus);

            var classArm = armCollections as IList<SubjectDto> ?? armCollections.ToList();
            var totalNoOfSubjects = classArm.Any() ? classArm.Count() : 0;
            ViewBag.totalNoOfSubjects = totalNoOfSubjects;
            if (classArm.Any())
            {
                return View(armCollections);
            }
            directional = 1;
            ShowMessage("Fresh class! Start adding Subjects", AlertType.Warning);
            return RedirectToAction("CreatClassSubject", new { subjectLevelStatus, subjectStatus, totalNoOfSubjects, directional });
        }

        public ActionResult CreatClassSubject(string subjectLevelStatus, SubjectStatus subjectStatus, byte totalNoOfSubjects = 0, byte directional = 0)
        {
            if (string.IsNullOrEmpty(subjectLevelStatus))
            {
                ViewBag.subjectLevelStatus = subjectStatus.ToString() == "AllSeniors" ? "Senior General Subjects" : subjectStatus.ToString();
            }
            else
            {
                ViewBag.subjectLevelStatus = subjectLevelStatus;
            }
            ViewBag.subjectStatus = subjectStatus;
            totalNoOfSubjects = (byte)(totalNoOfSubjects + 1);
            ViewBag.totalNoOfSubjects = totalNoOfSubjects;
            ViewBag.directional = directional;

            var model = new SubjectDto();
            ModelState.Clear();
            return View(model);
        }
        [HttpPost]
        public ActionResult CreatClassSubject(SubjectDto model, string subjectLevelStatus, SubjectStatus subjectStatus)
        {
            ViewBag.subjectName = model.SubjectName;
            ViewBag.subjectStatus = subjectStatus;

            if (string.IsNullOrEmpty(subjectLevelStatus))
            {
                ViewBag.subjectLevelStatus = subjectStatus.ToString() == "AllSeniors" ? "Senior General Subjects" : subjectStatus.ToString();
            }
            else
            {
                ViewBag.subjectLevelStatus = subjectLevelStatus;
            }
            if (ModelState.IsValid)
            {
                var verifyUniques = false;
                if (subjectStatus == SubjectStatus.PlayClass)
                {
                    verifyUniques = _registrationService.DoesThisPlayClassSubjectAreadyExist(model.SubjectName);
                }
                else if (subjectStatus == SubjectStatus.PreNursery)
                {
                    verifyUniques = _registrationService.DoesThisPreNurserySubjectAreadyExist(model.SubjectName);
                }
                else if (subjectStatus == SubjectStatus.General)
                {
                    verifyUniques = _registrationService.DoesThisGeneralSubjectAreadyExist(model.SubjectName);
                }

                else if (subjectStatus == SubjectStatus.Nursery)
                {
                    verifyUniques = _registrationService.DoesThisNurserySubjectAreadyExist(model.SubjectName);
                }
                else if (subjectStatus == SubjectStatus.Primary)
                {
                    verifyUniques = _registrationService.DoesThisPrimarySubjectAlreadyExist(model.SubjectName);
                }
                else if (subjectStatus == SubjectStatus.Jss)
                {
                    verifyUniques = _registrationService.DoesThisJssSubjectAlreadyExist(model.SubjectName);
                }
                else if (subjectStatus == SubjectStatus.AllSeniors)
                {
                    verifyUniques = _registrationService.DoesThisAllSeniorsSubjectAlreadyExist(model.SubjectName);
                }
                else if (subjectStatus == SubjectStatus.Science)
                {
                    verifyUniques = _registrationService.DoesThisScienceSubjectAlreadyExist(model.SubjectName);
                }
                else if (subjectStatus == SubjectStatus.Art)
                {
                    verifyUniques = _registrationService.DoesThisArtSubjectAlreadyExist(model.SubjectName);
                }
                else if (subjectStatus == SubjectStatus.Commercial)
                {
                    verifyUniques = _registrationService.DoesThisCommercialSubjectAlreadyExist(model.SubjectName);
                }
                else if (subjectStatus == SubjectStatus.Elective)
                {
                    verifyUniques = _registrationService.DoesThisElectiveSubjectAlreadyExist(model.SubjectName);
                }

                if (verifyUniques)
                {
                    model.TimeStatus = TimeStatus.Current;
                    model.SubjectName = !string.IsNullOrWhiteSpace(model.SubjectName) ? model.SubjectName.Replace(@"\", "") : "";
                    var collections = _registrationService.CreatClassSubject(model, subjectStatus);
                    if (collections)
                    {
                        UnitOfWork.Commit();
                        return RedirectToAction("SubjectRegIndex", new { subjectLevelStatus, subjectStatus }); //, classId 
                    }
                }
                ShowMessage($"This Subject \"{model.SubjectName}\" already exist.", AlertType.Danger);

                return View(model);
            }
            return View(model);
        }
        public ActionResult AllClassesAndArms()
        {
            var allArm = _registrationService.GetAllClasssAndArm();
            return View(allArm.Any() ? allArm : new List<ClassArmDto>());
        }

        //public ActionResult StudentsRegIndex(byte directional = 0)
        //{
        //    var stuff = _registrationService.StudentIndexClassAndArmDropDown();
        //    ViewBag.armName = stuff.ArmName;
        //    ViewBag.classes = stuff.ClassName;

        //    ViewBag.directional = directional;

        //    if (directional == 1)
        //    {
        //        directional = 2;
        //        return RedirectToAction("Index", new { });
        //    }

        //    var studentCollections = _registrationService.GetRecentlySavedStudents();
        //    var studentList = studentCollections as IList<StudentDetailsDto> ?? studentCollections.ToList();
        //    var totalNoOfArms = studentList.Any() ? studentList.Count() : 0;
        //    ViewBag.totalNoOfArms = totalNoOfArms;
        //    if (studentList.Any())
        //    {
        //        return View(studentCollections);
        //    }
        //    directional = 1;
        //    ShowMessage("Fresh class! Start adding Student", AlertType.Warning);
        //    return RedirectToAction("CreateStudent2Nd", "Registration", new { Area = "", directional });
        //}

        public ActionResult SubjectIndex()
        {

            var getSchoolLabel = _registrationService.GetSchoolLabel();
            if (getSchoolLabel != null && getSchoolLabel.Any())
            {
                foreach (var item in getSchoolLabel)
                {
                    if (item.SubjectStatus == SubjectStatus.General)
                    {
                        ViewBag.generalSujectLabel = item.SubjectStatusLabel;
                    }
                    else if (item.SubjectStatus == SubjectStatus.Elective)
                    {
                        ViewBag.electiveLabel = item.SubjectStatusLabel;
                    }
                    else if (item.SubjectStatus == SubjectStatus.PlayClass)
                    {
                        ViewBag.playClassLabel = item.SubjectStatusLabel;
                    }
                    else if (item.SubjectStatus == SubjectStatus.PreNursery)
                    {
                        ViewBag.preNurseryLabel = item.SubjectStatusLabel;
                    }
                    else if (item.SubjectStatus == SubjectStatus.Nursery)
                    {
                        ViewBag.nurseryLabel = item.SubjectStatusLabel;
                    }
                    else if (item.SubjectStatus == SubjectStatus.Primary)
                    {
                        ViewBag.primaryLabel = item.SubjectStatusLabel;
                    }
                    else if (item.SubjectStatus == SubjectStatus.Jss)
                    {
                        ViewBag.jssLabel = item.SubjectStatusLabel;
                    }
                    else if (item.SubjectStatus == SubjectStatus.AllSeniors)
                    {
                        ViewBag.allSeniorsLabel = item.SubjectStatusLabel;
                    }
                    else if (item.SubjectStatus == SubjectStatus.Science)
                    {
                        ViewBag.scienceLabel = item.SubjectStatusLabel;
                    }
                    else if (item.SubjectStatus == SubjectStatus.Elective)
                    {
                        ViewBag.electiveLabel = item.SubjectStatusLabel;
                    }
                }

                //ViewBag.religionSubjectLabel = getSchoolLabel.ReligionSubjectLabel;
                //ViewBag.preNurseryLabel = getSchoolLabel.PreNurseryLabel;
                //ViewBag.nurseryLabel = getSchoolLabel.NurseryLabel;
                //ViewBag.primaryLabel = getSchoolLabel.PrimaryLabel;Test
                //ViewBag.jssLabel = getSchoolLabel.JssLabel;
                //ViewBag.ssLabel = getSchoolLabel.SsLabel;
                //ViewBag.customLevelLabel = getSchoolLabel.CustomLevelLabel;
            }
            return View();
        }

        public ActionResult StudentDetails(string id)
        {
            var studentDetails = _registrationService.GetStudentDetails(id);
            return PartialView(studentDetails);
        }
        public ActionResult TeacherIndex()
        {
            //var check = _registrationService.HasSchoolBeenRegistered();
            //if (!check)
            //{
            //    ShowMessage("Sorry! You need to first register your school before continuing.", AlertType.Warning);
            //    return RedirectToAction("SchoolRegistration", "Registry", new { Area = "Registry" });
            //}
            // var teachersList = _registrationService.GetSubjectTeachers();
            return View();
        }
        public ActionResult AssignSubjectIndex(string teachersName, int? teacherId)
        {
            var stuff = _registrationService.GetTeachersClassesAndSujects();
            //ViewBag.subjects = stuff.SubjectsName;
            ViewBag.classes = stuff.ClassName;


            if (!string.IsNullOrEmpty(teacherId.ToString()))
            {
                ViewBag.teachersName = teachersName;
                ViewBag.teachers = teacherId;
            }
            else
            {
                ViewBag.teachers = stuff.TeachersName.OrderBy(x => x.Text);
            }

            return View();
        }
        public ActionResult RenderSelectArmsForArmsAssignment(string concatParameters)
        {
            if (string.IsNullOrEmpty(concatParameters))
            {
                ShowMessage("Assignment failed!", AlertType.Danger);
                return PartialView("RenderSelectArmsForSubjectAssignment", new List<ClassArmDto>());
            }
            //parameterList[0] is a dynamic value which can represent subjectId or teacherId. 
            //When the int.TryParse return false then parameterList[0] represent teacherId which is a string value
            var model = _registrationService.GetArmByClassIdAgainstSelectSubject(concatParameters);
            ViewBag.selectedId = concatParameters;
            return PartialView("RenderSelectArmsForSubjectAssignment", model);
        }

        public ActionResult AssignClassArm(string concatParameters)
        {
            var returnCollection = _registrationService.AssignTeacherSubjectArm(concatParameters);
            if (returnCollection.Any())
            {
                //ShowMessage("Assignment Succeeded", AlertType.Success);
                return PartialView("RenderSelectArmsForSubjectAssignment", returnCollection);
            }
            //ShowMessage("Assignment failed!", AlertType.Success);
            return PartialView("RenderSelectArmsForSubjectAssignment", new List<ClassArmDto>());
        }

        public ActionResult AssignedTeacherClassAndSubject()
        {
            var modelCollection = _registrationService.GetAssignedTeacherClassAndSubject();
            return View(modelCollection);
        }
        public ActionResult UnAssignSubjectTeacher(int id)
        {
            var status = _registrationService.UnAssignSubjectTeacher(id);
            ViewBag.Message = "You have successfully undone the Assignment";
            return PartialView("SubjectTeachers", status);
        }

        public ActionResult UnAssignClassTeacher(int id)
        {
            var status = _registrationService.UnAssignClassTeacher(id);

            if (status != null)
            {
                ViewBag.teacherStatus = "Form";
                ViewBag.Message = "You have successfully undone the Assignment";
                return PartialView("FormTeachers", status);
            }
            ViewBag.teacherStatus = "Form";
            //ShowMessage("Failed! Unable to complete the process.", AlertType.Danger);
            return PartialView("FormTeachers", new List<TeacherClassArmAssignmentDto>());
        }
        public ActionResult StudentAccessmentSystemInitialPreparation()
        {
            return View();
        }
        private bool Ty(IOrderedEnumerable<TotalStudentListToPdfDto> studentList, int total, string pv, SystemImplementationType implementationStatus = SystemImplementationType.PerStudentPerTerm)//RenderStudentsListToPdfAndMail
        {
            BaseFont ftntHead = BaseFont.CreateFont(BaseFont.TIMES_ROMAN, BaseFont.CP1250, BaseFont.NOT_EMBEDDED);
            Font fntHead = new Font(ftntHead, 16, 1, BaseColor.GRAY);
            BaseFont headTextColor = BaseFont.CreateFont(BaseFont.TIMES_ROMAN, BaseFont.CP1250, BaseFont.NOT_EMBEDDED);
            Font headfontColor = new Font(headTextColor, 16, 1, BaseColor.RED);

            Paragraph pageHeading = new Paragraph { Alignment = Element.ALIGN_CENTER };

            pageHeading.Add(new Chunk(studentList.First().SchoolInfo.SchoolName, headfontColor));
            pageHeading.Add(new Chunk("\n", fntHead));
            pageHeading.Add(new Chunk(studentList.First().SchoolInfo.SchoolAddress, headfontColor));
            pageHeading.Add(new Chunk("\n", fntHead));

            pageHeading.Add(new Chunk(studentList.First().SchoolInfo.Telephone + ", " +
                studentList.First().SchoolInfo.PoAddress + ", " +
                studentList.First().SchoolInfo.EmailAddress + ", " +
                studentList.First().SchoolInfo.Website, headfontColor));


            pageHeading.Add(new Chunk("\n", fntHead));
            pageHeading.Add(new Chunk("Date/Time of execution: " + DateTime.Now, fntHead));

            pageHeading.Add(new Chunk("\n", fntHead));
            pageHeading.Add(new Chunk("\n", fntHead));
            pageHeading.Add(new Chunk("School Student List", fntHead));

            Document pdfDoc = new Document(PageSize.A4);
            PdfWriter.GetInstance(pdfDoc, Response.OutputStream);
            MemoryStream memoryStream = new MemoryStream();
            PdfWriter writer = PdfWriter.GetInstance(pdfDoc, memoryStream); //capture the object

            PdfPTable pdf = new PdfPTable(5)
            {
                HorizontalAlignment = Element.ALIGN_CENTER,
                TotalWidth = 460f,
                LockedWidth = true
            };

            float[] widths = new float[] { 60f, 85f, 200f, 85f, 35f };
            pdf.SetWidths(widths);

            Font font = new Font();
            font.Color = new BaseColor(Color.White);

            PdfPCell sNo1 = new PdfPCell(new Phrase("S/No.", font));
            sNo1.BackgroundColor = BaseColor.BLACK;

            PdfPCell id1 = new PdfPCell(new Phrase("Admisn No.", font));
            id1.BackgroundColor = BaseColor.BLACK;

            PdfPCell fullNamme1 = new PdfPCell(new Phrase("Full Name", font));
            fullNamme1.BackgroundColor = BaseColor.BLACK;

            PdfPCell class1 = new PdfPCell(new Phrase("Class", font));
            class1.BackgroundColor = BaseColor.BLACK;

            PdfPCell arm1 = new PdfPCell(new Phrase("Arm", font));

            arm1.BackgroundColor = BaseColor.BLACK;

            pdf.AddCell(sNo1);
            pdf.AddCell(id1);
            pdf.AddCell(fullNamme1);
            pdf.AddCell(class1);
            pdf.AddCell(arm1);
            var counter = 0;

            foreach (var eachArm in studentList)
            {
                counter = counter + 1;
                PdfPCell sNo = new PdfPCell(new Phrase(counter.ToString()));
                PdfPCell id = new PdfPCell(new Phrase(eachArm.IdNo));
                PdfPCell fullName = new PdfPCell(new Phrase(eachArm.FullName));
                PdfPCell classLabel = new PdfPCell(new Phrase(eachArm.ClassLabel));
                PdfPCell arm = new PdfPCell(new Phrase(eachArm.Arm));
                pdf.AddCell(sNo);
                pdf.AddCell(id);
                pdf.AddCell(fullName);
                pdf.AddCell(classLabel);
                pdf.AddCell(arm);
            }
            var fileName = studentList.First().SchoolInfo.SchoolName;
            try
            {
                pdfDoc.Open();
                pdfDoc.Add(pageHeading);
                pdfDoc.Add(new Chunk("\n", fntHead));
                pdfDoc.Add(new Chunk("The total number of student is: " + total, headfontColor));
                pdfDoc.Add(pdf);
                writer.CloseStream = false;
                pdfDoc.Close();
                memoryStream.Position = 0;

                if (implementationStatus == SystemImplementationType.PerStudentPerTerm)
                {
                    var securityService = ServiceLocator.Get<ISecurityService>();
                    var credentials = securityService.GetCredentials();
                    if (credentials != null)
                    {
                        var mm = new MailMessage(credentials.From, credentials.To);
                        mm.Subject = "New Assessment preparation on " + DateTime.Now;
                        mm.Attachments.Add(new Attachment(memoryStream, fileName + ".pdf"));
                        mm.Body = "NAITS-ASMS Subscription renewal";
                        mm.IsBodyHtml = true;

                        string msg = SendEmailWithAttachment(mm, credentials, int.Parse(credentials.Ports[0]), pdfDoc, fileName);//8889, 25

                        if (msg == "failed" && credentials.Ports.Count > 1)
                        {
                            mm = new MailMessage(credentials.From, credentials.To);

                            mm.Subject = "New Assessment preparation on " + DateTime.Now;
                            mm.Attachments.Add(new Attachment(memoryStream, fileName + ".pdf"));
                            mm.Body = "NAITS-ASMS Subscription renewal";
                            mm.IsBodyHtml = true;
                            msg = SendEmailWithAttachment(mm, credentials, int.Parse(credentials.Ports[1]), pdfDoc, fileName);//8889, 25
                            if (msg == "failed")
                            {
                                Response.ContentType = "application/pdf";
                                Response.AppendHeader("content-disposition", "attachment; filename=" + fileName + ".pdf");
                                Response.Write(pdfDoc);
                                if (pdfDoc.IsOpen())
                                {
                                    pdfDoc.Close();
                                }
                                Response.Flush();
                                Response.End();
                            }
                        }
                    }
                    else
                    {
                        ShowMessage("Failed! Error in the email parameters of the config file.", AlertType.Danger);
                    }
                }
                else
                {
                    Response.ContentType = "application/pdf";
                    Response.AppendHeader("content-disposition", "attachment; filename=" + fileName + ".pdf");
                    Response.Write(pdfDoc);
                    if (pdfDoc.IsOpen())
                    {
                        pdfDoc.Close();
                    }
                    Response.Flush();
                    Response.End();
                }
                return true;
            }
            catch (Exception ex)
            {
                if (pdfDoc.IsOpen())
                {
                    pdfDoc.Close();
                }
                return false;
            }
        }
        private string SendEmailWithAttachment(MailMessage mm, EmailCredentials credentials, int portNumber, Document pdfDoc, string fileName)
        {

            string messageString = "";
            using (mm)
            {
                using (SmtpClient sc = new SmtpClient(credentials.HostName, portNumber))
                {
                    sc.EnableSsl = credentials.EnableSsl;
                    sc.DeliveryMethod = SmtpDeliveryMethod.Network;
                    sc.UseDefaultCredentials = false;
                    sc.Timeout = 30000;
                    sc.Credentials = new NetworkCredential(credentials.UserName, credentials.Password);

                    var checkConnection = CheckForInternetConnection();
                    if (checkConnection)
                    {
                        try
                        {
                            sc.Send(mm);
                            Response.ContentType = "application/pdf";
                            Response.AppendHeader("content-disposition", "attachment; filename=" + fileName + ".pdf");
                            Response.Write(pdfDoc);
                            if (pdfDoc.IsOpen())
                            {
                                pdfDoc.Close();
                            }
                            Response.Flush();
                            Response.End();

                            messageString = "System Information was successfully submited.";
                        }
                        catch (Exception ex)
                        {
                            messageString = "failed";
                        }

                    }
                    else
                    {
                        messageString = "Internet Connection is required in order to submit the request.";
                    }
                }
            }
            return messageString;
        }

        public ActionResult CancelRecentPromotion()
        {
            var status = _registrationService.UndoRecentStudentsPromotion3rd();
            if (status)
            {
                UnitOfWork.Commit();
                return Json("Success", JsonRequestBehavior.AllowGet);
            }
            return Json("Failure", JsonRequestBehavior.AllowGet);
        }
        private static void TimeoutCallback(object state, bool timedOut)
        {
            if (timedOut)
            {
                HttpWebRequest request = state as HttpWebRequest;
                if (request != null)
                {
                    request.Abort();
                }
            }
        }
        public ActionResult StudentAccessmentSystemInitialPreparationPost(TermStatus term)
        {
            var securityService = ServiceLocator.Get<ISecurityService>();

            var bStatus = securityService.B();//B means VerifyAccess
            if (bStatus.AccessVerifyReturnValues != null)
            {

                ShowMessage(bStatus.AccessVerifyReturnValues.Value1, AlertType.Danger);
                return RedirectToAction(bStatus.AccessVerifyReturnValues.Value2,
                    bStatus.AccessVerifyReturnValues.Value3,
                    new { Area = bStatus.AccessVerifyReturnValues.Value4 });
            }

            if ((bStatus.LoginReturnStatus.ReturnMessage != "1"))
            {
                ShowMessage(bStatus.LoginReturnStatus.ReturnMessage, AlertType.Danger);
                return View("StudentAccessmentSystemInitialPreparation");
            }
            var checkConnectivity = CheckForInternetConnection();
            if (checkConnectivity)
            {
                var status = _registrationService.DoSystemInitialPreparationForStudentAccessment(term);
                if (status.StatusCode == "Succeed")
                {
                    var studentList =
                        status.TotalStudentListToPdf.OrderBy(d => d.CurrentClass).ThenBy(f => f.Arm);
                    var total = status.TotalStudentListToPdf?.Count;
                    if (total != null && total <= 0)
                    {
                        var checkConnectivition = CheckForInternetConnection();
                        var msg = securityService.ReturnMessage1();
                        if (checkConnectivition)
                        {
                            try
                            {
                                var pv = securityService.GetPv();
                                if (studentList.Any())
                                {
                                    var statusCode = Ty(studentList, (int)total, pv); //Yy means RenderStudentsListToPdfAndMail
                                    if (statusCode)
                                    {

                                        UnitOfWork.Commit();
                                        ShowMessage("Students assessment environment has successfully been prepared.",
                                            AlertType.Success);
                                    }
                                    else
                                    {
                                        ShowMessage(msg, AlertType.Warning);
                                    }
                                }
                                else
                                {
                                    ShowMessage(
                                        "Students assessment environment has successfully been prepared. You can now start adding student.",
                                        AlertType.Success);
                                }

                            }
                            catch (Exception ex)
                            {
                                ShowMessage(msg, AlertType.Warning);
                            }
                        }
                        else
                        {
                            ShowMessage(msg, AlertType.Warning);
                        }
                    }
                    else
                    {
                        UnitOfWork.Commit();
                        ShowMessage("Successful.", AlertType.Warning);
                    }
                }
                else
                {
                    ShowMessage(status.StatusCode, AlertType.Warning);
                }
            }
            else
            {
                ShowMessage(
                    $"Sorry! Internet Connection is required in order to prepare result processing environment.",
                    AlertType.Warning);
            }
            return View("StudentAccessmentSystemInitialPreparation");
        }
        public ActionResult SubjectTeachers()
        {
            var list = _registrationService.GetSubjectTeachers();
            ViewBag.teacherStatus = "Subject";
            return PartialView(list);
        }

        public ActionResult RegisteredTeacher()
        {
            var list = _registrationService.GetAllTeachers();
            return PartialView(list);
        }

        public ActionResult FormTeachers()
        {
            var formTeacher = _registrationService.GetClassTeachers();
            if (formTeacher != null)
            {
                ViewBag.teacherStatus = "Form";
                return PartialView(formTeacher);
            }
            ViewBag.teacherStatus = "Form";
            return PartialView(new List<TeacherClassArmAssignmentDto>());
        }

        public ActionResult SchoolRegistration()
        {
            var model = _registrationService.GetSchoolInfoAndProprietorCode();
            ViewBag.proprietorCode = model.ProprietorCode;
            return View(model.SchoolInfo);
        }
        [HttpPost]
        public ActionResult SchoolRegistration(SchoolInfoDto model, string schoolLogoStringOld, HttpPostedFileBase schoolLogoString)
        {
            ModelState.Remove("Id");
            if (ModelState.IsValid)
            {
                if (schoolLogoString != null)
                {
                    var fileSize = schoolLogoString.ContentLength;
                    if ((fileSize / 1024) > 16)
                    {
                        ShowMessage("Your school logo size is too big. The maximum size is 16kb", AlertType.Warning);
                        return View(model);
                    }
                    else
                    {
                        var uploadString = "";
                        var extension = "";
                        if (schoolLogoString.ContentType != "image/jpeg" && schoolLogoString.ContentType != "image/png")
                        {
                            ShowMessage("Only 'JPEG' and 'png' image are allowed.", AlertType.Danger);
                            return View(model);
                        }
                        extension = schoolLogoString.ContentType == "image/jpeg" ? ".jpg" : ".png";
                        if (schoolLogoStringOld == "System.Web.HttpPostedFileWrapper")
                        {
                            schoolLogoStringOld = Guid.NewGuid() + extension;
                        }
                        // var getFileName = Path.GetFileName(schoolLogoString.FileName);
                        if (System.IO.File.Exists(Path.Combine(Server.MapPath("~/Content/SchLogos/"), schoolLogoStringOld)))
                        {
                            System.IO.File.Delete(Path.Combine(Server.MapPath("~/Content/SchLogos/"), schoolLogoStringOld));
                        }

                        var getFileName = schoolLogoString.FileName;
                        var path = Path.Combine(Server.MapPath("~/Content/SchLogos"), Path.GetFileName(schoolLogoString.FileName));
                        schoolLogoString.SaveAs(path);

                        if (string.IsNullOrEmpty(schoolLogoStringOld))
                        {
                            var fileExtension =
                                Path.GetExtension(Path.Combine(Server.MapPath("~/Content/SchLogos/"), getFileName));
                            schoolLogoStringOld = Guid.NewGuid().ToString() + fileExtension;
                        }
                        else
                        {
                            var newString = schoolLogoStringOld.Split('.').ToList()[0];
                            schoolLogoStringOld = newString + extension;
                        }

                        uploadString = schoolLogoStringOld;
                        System.IO.File.Move(Path.Combine(Server.MapPath("~/Content/SchLogos/"), getFileName),
                            Path.Combine(Server.MapPath("~/Content/SchLogos/"), schoolLogoStringOld));
                        model.SchoolLogoString = uploadString;
                    }
                }
                else
                {
                    model.SchoolLogoString = schoolLogoStringOld;
                }
                var status = _registrationService.UpdateSchoolInfo(model);
                if (status)
                {
                    UnitOfWork.Commit();
                    ShowMessage("Your file upload has successfully been completed.", AlertType.Success);
                    return RedirectToAction("Index");
                }
            }
            return View(model);
        }

        public ActionResult AssignClassArmIndex()
        {
            var result = _registrationService.GetTeachersAndClasses();
            ViewBag.TeachersName = result.TeachersName.OrderBy(x => x.Text);
            ViewBag.classes = result.ClassName;
            return View();
        }

        public ActionResult OtherRegistration()
        {
            //var check = _registrationService.HasSchoolBeenRegistered();
            //if (!check)
            //{
            //    ShowMessage("Sorry! You need to first register your school before continuing.", AlertType.Warning);
            //    return RedirectToAction("SchoolRegistration", "Registry", new { Area = "Registry" });
            //}
            return View();
        }

        public ActionResult CreateShcoolHead()
        {
            return PartialView();
        }
        [HttpPost]
        public ActionResult CreateShcoolHead(SchoolHeadDto model, HttpPostedFileBase signatureImageString)
        {
            if (ModelState.IsValid)
            {
                if (signatureImageString != null)
                {
                    var fileSize = signatureImageString.ContentLength;
                    if ((fileSize / 1024) > 13)
                    {
                        ShowMessage("Your signature size is too big. The maximum size is 10kb", AlertType.Warning);
                        return View(model);
                    }
                    else
                    {
                        var uploadString = "";
                        if (signatureImageString.ContentType != "image/jpeg" && signatureImageString.ContentType != "image/png")
                        {
                            ShowMessage("Only 'JPEG' and 'png' image are allowed.", AlertType.Danger);
                            return View(model);
                        }
                        var getFileName = signatureImageString.FileName;
                        var path = Path.Combine(Server.MapPath("~/Content/Signatures"), Path.GetFileName(signatureImageString.FileName));
                        signatureImageString.SaveAs(path);

                        var fileExtension = Path.GetExtension(Path.Combine(Server.MapPath("~/Content/Signatures/"), getFileName));
                        var guidValue = Guid.NewGuid().ToString();
                        var newName = guidValue + fileExtension;
                        uploadString = newName;
                        System.IO.File.Move(Path.Combine(Server.MapPath("~/Content/Signatures/"), getFileName),
                            Path.Combine(Server.MapPath("~/Content/Signatures/"), newName));
                        model.SignatureImageString = uploadString;
                    }
                }

                var result = _registrationService.CreatSchoolHead(model);
                if (result)
                {
                    ShowMessage("The Principal information has successfully been posted.", AlertType.Success);
                    UnitOfWork.Commit();
                    return RedirectToAction("SchoolHeadIndex", model);
                }
            }
            return View(model);
        }
        public ActionResult ResumptionDate()
        {
            return View();
        }
        [HttpPost]
        public ActionResult ResumptionDate(DateTime resumptionDate)
        {
            var status = _registrationService.CreateResumptionDate(resumptionDate);
            if (status)
            {
                ShowMessage("The next term date has successfully been submited.", AlertType.Success);
                UnitOfWork.Commit();
            }
            else
            {
                ShowMessage("Continuous assessment intitial background preparation needs to be done before this.", AlertType.Warning);
            }
            return View();
        }

        public ActionResult SchoolHeadIndex()
        {
            var list = _registrationService.GetPrincals();
            return View(list);
        }
        [HttpPost]
        public JsonResult NextTermResumptionDate()
        {
            var status = _registrationService.GetResumptionDate();
            // var status1 = DateTime.Now.ToLongDateString();
            if (status != null)
            {
                var dateString = status.Value.Date.ToLongDateString();
                return Json("Next term begins on: " + dateString, JsonRequestBehavior.AllowGet);
            }
            return Json("No set date yet.", JsonRequestBehavior.DenyGet);
        }

        public ActionResult EditSchoolHeadDetails(string concatValues)
        {
            if (string.IsNullOrEmpty(concatValues))
            {
                return View(new SchoolHeadDto());
            }
            var eachValue = concatValues.Split(',');
            TimeStatus status;
            HeadStatus headStatus;
            var gender = eachValue[3] == Gender.Male.ToString() ? Gender.Male : Gender.Female;
            status = eachValue[4] == TimeStatus.Current.ToString() ? TimeStatus.Current : TimeStatus.Previous;
            headStatus = eachValue[5] == HeadStatus.HeadTeacher.ToString() ? HeadStatus.HeadTeacher : HeadStatus.Principal;


            ViewBag.timeStatus = _registrationService.TimeStatus(status);
            ViewBag.headStatus = _registrationService.HeadStatu(headStatus);
            ViewBag.gender = _registrationService.Gendar(gender);
            var model = new SchoolHeadDto
            {
                Id = short.Parse(eachValue[0]),
                HeadsName = eachValue[1],
                PhoneNumber = eachValue[2],
                Status = status,
                SignatureImageString = eachValue[6]
            };
            return PartialView(model);
        }

        [HttpPost]
        public ActionResult EditSchoolHeadDetails(SchoolHeadDto model, string signatureImageStringOld, HttpPostedFileBase signatureImageString)
        {
            if (ModelState.IsValid)
            {
                if (signatureImageString != null)
                {
                    var fileSize = signatureImageString.ContentLength;
                    if ((fileSize / 1024) > 13)
                    {
                        TimeStatus status = model.Status;
                        HeadStatus headStatus = model.HeadStatus == HeadStatus.HeadTeacher ? HeadStatus.HeadTeacher : HeadStatus.Principal;

                        ViewBag.timeStatus = _registrationService.TimeStatus(status);
                        ViewBag.headStatus = _registrationService.HeadStatu(headStatus);

                        ShowMessage("Your signature size is too big. The maximum size is 10kb", AlertType.Warning);
                        return View(model);
                    }
                    else
                    {
                        var uploadString = "";
                        if (signatureImageString.ContentType != "image/jpeg" && signatureImageString.ContentType != "image/png")
                        {
                            ShowMessage("Only 'JPEG' and 'png' image are allowed.", AlertType.Danger);
                            return View(model);
                        }
                        var extension = signatureImageString.ContentType == "image/jpeg" ? ".jpg" : ".png";
                        if (signatureImageStringOld == "System.Web.HttpPostedFileWrapper")
                        {
                            signatureImageStringOld = Guid.NewGuid() + extension;
                        }

                        if (System.IO.File.Exists(Path.Combine(Server.MapPath("~/Content/Signatures/"), signatureImageStringOld)))
                        {
                            System.IO.File.Delete(Path.Combine(Server.MapPath("~/Content/Signatures/"), signatureImageStringOld));
                        }

                        var getFileName = signatureImageString.FileName;
                        var path = Path.Combine(Server.MapPath("~/Content/Signatures"), Path.GetFileName(signatureImageString.FileName));
                        signatureImageString.SaveAs(path);

                        uploadString = signatureImageStringOld;
                        System.IO.File.Move(Path.Combine(Server.MapPath("~/Content/Signatures/"), getFileName),
                            Path.Combine(Server.MapPath("~/Content/Signatures/"), signatureImageStringOld));
                        model.SignatureImageString = uploadString;
                    }
                }
                else
                {
                    model.SignatureImageString = signatureImageStringOld;
                }
                var status1 = _registrationService.UpdateSchoolHeadInfo(model);
                if (status1)
                {
                    UnitOfWork.Commit();
                    ShowMessage("Update Successful", AlertType.Success);
                    return RedirectToAction("SchoolHeadIndex");
                }
            }
            return View(model);
        }
        public ActionResult EditTeacher(int teacherId)
        {
            var model = _registrationService.GetTeacherById(teacherId);
            ViewBag.timeStatus = _registrationService.TimeStatus(model.TimeStatus);
            ViewBag.EmployeeStatus = _registrationService.EmployeeStatus(model.EmployeeStatus);
            ViewBag.gender = _registrationService.Gendar(model.Gender);
            return View(model);
        }
        [HttpPost]
        public ActionResult EditTeacher(UserDto model,
                        string signatureImageStringOld,
                        HttpPostedFileBase signatureImageString,
                        string signatureImageStringBase64)
        {

            if (string.IsNullOrWhiteSpace(model.Password) && string.IsNullOrWhiteSpace(model.ConfirmPassword))
            {
                ModelState.Remove("Password");
                ModelState.Remove("ConfirmPassword");
            }
            ViewBag.timeStatus = _registrationService.TimeStatus(model.TimeStatus);
            ViewBag.gender = _registrationService.Gendar(model.Gender);
            ViewBag.EmployeeStatus = _registrationService.EmployeeStatus(model.EmployeeStatus);

            if (ModelState.IsValid)
            {
                if (signatureImageString != null)
                {
                    var fileSize = signatureImageString.ContentLength;
                    if ((fileSize / 1024) > 13)
                    {
                        ShowMessage("Your signature size is too big. The maximum size is 10kb", AlertType.Warning);
                        return View(model);
                    }
                    else
                    {
                        if (signatureImageString.ContentType != "image/jpeg" && signatureImageString.ContentType != "image/png")
                        {
                            ShowMessage("Only 'JPEG' and 'png' image are allowed.", AlertType.Danger);
                            return View(model);
                        }
                        var extension = signatureImageString.ContentType == "image/jpeg" ? ".jpg" : ".png";
                        if (signatureImageStringOld == "System.Web.HttpPostedFileWrapper")
                        {
                            signatureImageStringOld = Guid.NewGuid() + extension;
                        }

                        if (!string.IsNullOrEmpty(signatureImageStringOld))
                        {
                            if (System.IO.File.Exists(Path.Combine(Server.MapPath("~/Content/Signatures/"), signatureImageStringOld)))
                            {
                                System.IO.File.Delete(Path.Combine(Server.MapPath("~/Content/Signatures/"), signatureImageStringOld));
                            }
                        }

                        var getFileName = signatureImageString.FileName;
                        var path = Path.Combine(Server.MapPath("~/Content/Signatures"), Path.GetFileName(signatureImageString.FileName));
                        signatureImageString.SaveAs(path);
                        var newName = "";

                        if (string.IsNullOrEmpty(signatureImageStringOld))
                        {
                            var getName = Path.GetFileName(path);
                            var fileExtension = Path.GetExtension(Path.Combine(Server.MapPath("~/Content/Signatures/"), getName));
                            newName = Guid.NewGuid() + fileExtension;
                        }
                        else
                        {
                            var newString = signatureImageStringOld.Split('.').ToList()[0];
                            signatureImageStringOld = newString + extension;
                            newName = signatureImageStringOld;
                        }
                        System.IO.File.Move(Path.Combine(Server.MapPath("~/Content/Signatures/"), getFileName),
                                Path.Combine(Server.MapPath("~/Content/Signatures/"), newName));
                        model.SignatureImageString = newName;
                    }
                }
                else
                {
                    model.SignatureImageString = signatureImageStringOld;
                }
                model.Email = !string.IsNullOrWhiteSpace(model.Email) ? model.Email : "contact@gmail.com";
                var registrationServise = _registrationService;
                var status = registrationServise.UpdateTeacherInfo(model);
                if (status == 0)
                {
                    UnitOfWork.Commit();
                    ShowMessage("Update Successful", AlertType.Success);
                    return RedirectToAction("TeacherIndex", "Registry", new { Area = "Registry" });
                }
                else if (status == 1 || status == 2)
                {
                    ShowMessage("You cannot change teacher's status to previous while he still a subject Teacher or a form master", AlertType.Warning);
                }

                else if (status == 3)
                {
                    ShowMessage("Failed! Email exist agaist another record.", AlertType.Warning);
                }
                else if (status == 4)
                {
                    ShowMessage("Failed! Username exist agaist another user.", AlertType.Warning);
                }
            }
            return View(model);
        }
        public ActionResult EditSubject(string concatParam)//int subjectid, string subjectname, short sno, SubjectStatus subjectstatus
        {
            if (string.IsNullOrEmpty(concatParam))
            {
                return RedirectToAction("SubjectRegIndex");
            }
            var itemToList = concatParam.Split(',').ToList();
            var model = ServiceLocator.Get<SubjectDto>();
            model.Id = int.Parse(itemToList[0]);
            model.SubjectName = itemToList[1];
            //model.SNo = short.Parse(itemToList[2]);
            var status = ((SubjectStatus)Enum.Parse(typeof(SubjectStatus), itemToList[2]));
            model.TimeStatus = ((TimeStatus)Enum.Parse(typeof(TimeStatus), itemToList[3]));
            model.SubjectCategoryId = short.Parse(itemToList[5]);
            ViewBag.subjectStatus = status;
            ViewBag.subjectLevelStatus = itemToList[4];
            //model.SubjectCategory.SubjectStatus = status;
            return PartialView(model);
        }

        [HttpPost]
        public JsonResult EditSubject(SubjectDto model, SubjectStatus subjectStatus)
        {
            if (ModelState.IsValid)
            {
                var verifyUniques = false;
                if (subjectStatus == SubjectStatus.PlayClass)
                {
                    verifyUniques = _registrationService.PlayClassSubjectUpdateValidation(model.SubjectName, model.Id);
                }
                else if (subjectStatus == SubjectStatus.PreNursery)
                {
                    verifyUniques = _registrationService.PreNurserySubjectUpdateValidation(model.SubjectName, model.Id);
                }
                else if (subjectStatus == SubjectStatus.General)
                {
                    verifyUniques = _registrationService.GeneralSubjectUpdateValidation(model.SubjectName, model.Id);
                }
                else if (subjectStatus == SubjectStatus.Nursery)
                {
                    verifyUniques = _registrationService.NurserySubjectUpdateValidation(model.SubjectName, model.Id);
                }
                else if (subjectStatus == SubjectStatus.Primary)
                {
                    verifyUniques = _registrationService.PrimarySubjectUpdateValidation(model.SubjectName, model.Id);
                }
                else if (subjectStatus == SubjectStatus.Jss)
                {
                    verifyUniques = _registrationService.JssSubjectUpdateValidation(model.SubjectName, model.Id);
                }
                else if (subjectStatus == SubjectStatus.AllSeniors)
                {
                    verifyUniques = _registrationService.AllSeniorSubjectUpdateValidation(model.SubjectName, model.Id);
                }
                else if (subjectStatus == SubjectStatus.Science)
                {
                    verifyUniques = _registrationService.ScienceSubjectUpdateValidation(model.SubjectName, model.Id);
                }
                else if (subjectStatus == SubjectStatus.Art)
                {
                    verifyUniques = _registrationService.ArtSubjectUpdateValidation(model.SubjectName, model.Id);
                }
                else if (subjectStatus == SubjectStatus.Elective)
                {
                    verifyUniques = _registrationService.ElectiveSubjectUpdateValidation(model.SubjectName, model.Id);
                }
                else if (subjectStatus == SubjectStatus.Commercial)
                {
                    verifyUniques = _registrationService.CommercialSubjectUpdateValidation(model.SubjectName, model.Id);
                }
                if (verifyUniques)
                {
                    model.SubjectName = !string.IsNullOrWhiteSpace(model.SubjectName)
                        ? model.SubjectName.Replace(@"\", "")
                        : "";
                    var status = _registrationService.UpdateSubject(model, subjectStatus);
                    if (status)
                    {
                        UnitOfWork.Commit();
                        return Json(true, JsonRequestBehavior.AllowGet);
                    }
                }
                else
                {
                    var isRequestUpdateStatus = _registrationService.IsRequestToUpdateSubjectTimeStatus(model.Id, model.TimeStatus);
                    if (isRequestUpdateStatus)
                    {
                        model.SubjectName = !string.IsNullOrWhiteSpace(model.SubjectName)
                       ? model.SubjectName.Replace(@"\", "")
                       : "";
                        var status = _registrationService.UpdateSubject(model, subjectStatus);
                        if (status)
                        {
                            UnitOfWork.Commit();
                            return Json(true, JsonRequestBehavior.AllowGet);
                        }
                    }
                }
            }
            return Json(false, JsonRequestBehavior.DenyGet);
        }
        [HttpPost]
        public ActionResult DeleteSubject(int id)
        {
            var status = _registrationService.DeleteSubject(id);
            if (status)
            {
                UnitOfWork.Commit();
                return Json(true, JsonRequestBehavior.AllowGet);
            }
            return Json(false, JsonRequestBehavior.DenyGet);
        }

        public ActionResult EditStudentInfo(string id)
        {
            var model = _registrationService.GetStudentById(id);

            ViewBag.currentClass = _registrationService.CurrentsClass(model.ClassArm.Classs.ClassName);
            var arm = _registrationService.ClasssArms(model.ClassArm.ArmName);
            if (string.IsNullOrEmpty(arm.First().Text))
            {
                ShowMessage("Please create 'Class Arm' before attempt adding student.", AlertType.Warning);
                return RedirectToAction("ClassRegIndex");
            }
            ViewBag.classArm = arm;
            ViewBag.startClass = _registrationService.StartsClass(model.StartClass);
            ViewBag.timeStatus = _registrationService.StudentStatus(model.Status);
            ViewBag.gender = _registrationService.Gendar(model.User.Gender);
            ViewBag.studentStatus = _registrationService.StudentStatus(model.ClassCategory);
            ViewBag.religion = _registrationService.ReligionStatus(model.Religion);

            return View(model);
        }
        [HttpPost]
        public ActionResult EditStudentInfo(ArmName classAarm, StudentDetailsDto model, HttpPostedFileBase passport, string passportStringOld, int? directional = 0)
        {
            ViewBag.currentClass = _registrationService.CurrentsClass(model.CurrentClass);
            ViewBag.classArm = _registrationService.ClasssArms(classAarm);
            ViewBag.startClass = _registrationService.StartsClass(model.StartClass);
            ViewBag.timeStatus = _registrationService.StudentStatus(model.Status);
            ViewBag.gender = _registrationService.Gendar(model.Gender);
            ViewBag.studentStatus = _registrationService.StudentStatus(model.ClassCategory);
            ViewBag.religion = _registrationService.ReligionStatus(model.Religion);

            ModelState.Remove("Passport");
            //ModelState.Remove("User.UserName");
            //ModelState.Remove("User.Email");
            //ModelState.Remove("User.Password");
            //ModelState.Remove("User.ConfirmPassword");
            ModelState.Remove("FirstName");
            ModelState.Remove("SurName");
            //var error = ModelState.Values.Where(x => x.Errors.Count >= 1);
            if (ModelState.IsValid)
            {
                var verifyUniques = _registrationService.StudentUpdateValidationChecks(model.Id, model.IdNo, model.CurrentClass, model.StartClass, classAarm, model.EmailAddress, model.UserId, model.ClassCategory);//, model.StartClass, aarmName
                switch (verifyUniques)
                {
                    case 1:
                        ShowMessage($"You need to register the Class \"{model.StartClass}\" before adding Student to it.", AlertType.Warning);
                        model.Passport = passportStringOld;
                        return View(model);
                    case 2:
                        ShowMessage($"You need to register the Class \"{model.CurrentClass}\" before adding Student to it.", AlertType.Warning);
                        model.Passport = passportStringOld;
                        return View(model);
                    case 3:
                        ShowMessage($"You need to register the Arm \"{classAarm}\" before adding Student to it.", AlertType.Warning);
                        model.Passport = passportStringOld;
                        return View(model);
                    case 4:
                        ShowMessage($"Failed! This Id \"{model.IdNo}\" has been used for another student.", AlertType.Warning);
                        model.Passport = passportStringOld;
                        return View(model);
                    case 5:
                        ShowMessage("Failed! Your class selection does not match the category. kindly correct the class and its category to match correctly.", AlertType.Danger);
                        model.Passport = passportStringOld;
                        return View(model);
                    default:
                        if (passport != null)
                        {
                            var fileSize = passport.ContentLength;
                            if ((fileSize / 1024) > 15)
                            {
                                ShowMessage("Your passport size is too big. The maximum size is 14kb", AlertType.Warning);
                                return View(model);
                            }
                            else
                            {
                                if (passport.ContentType != "image/jpeg" && passport.ContentType != "image/png")
                                {
                                    ShowMessage("Only 'JPEG' and 'png' image are allowed.", AlertType.Danger);
                                    model.Passport = passportStringOld;
                                    return View(model);
                                }
                                //check if file exist, delete it.
                                if (System.IO.File.Exists(Path.Combine(Server.MapPath("~/Passport/"), passportStringOld)))
                                {
                                    System.IO.File.Delete(Path.Combine(Server.MapPath("~/Passport/"), passportStringOld));
                                }
                                var path = Path.Combine(Server.MapPath("~/Passport"), Path.GetFileName(passport.FileName));
                                passport.SaveAs(path);

                                var getName = Path.GetFileName(path);

                                var fileExtension = Path.GetExtension(Path.Combine(Server.MapPath("~/Passport/"), getName));
                                var guidValue = Guid.NewGuid().ToString();
                                var newName = guidValue + fileExtension;
                                System.IO.File.Move(Path.Combine(Server.MapPath("~/Passport/"), getName),
                                    Path.Combine(Server.MapPath("~/Passport/"), newName));
                                model.Passport = newName;
                            }
                        }
                        else
                        {
                            model.Passport = passportStringOld;
                        }
                        model.FirstName = !string.IsNullOrWhiteSpace(model.FirstName)? model.FirstName.Replace(@"\", "") : "";
                        model.MiddleName = !string.IsNullOrWhiteSpace(model.MiddleName) ? model.MiddleName.Replace(@"\", "") : "";
                        model.SurName = !string.IsNullOrWhiteSpace(model.SurName) ? model.SurName.Replace(@"\", "") : "";

                        model.IdNo = !string.IsNullOrWhiteSpace(model.IdNo) ? model.IdNo.Replace(@"\", "") : "";

                        var status = _registrationService.UpdateStudentDetails(model, classAarm);
                        if (status == "")
                        {
                            UnitOfWork.Commit();
                            ShowMessage("Student Registration Successful", AlertType.Success);
                            return RedirectToAction("StudentsRegIndex", new { directional });
                        }
                        else
                        {
                            UnitOfWork.Rollback();
                            ShowMessage(status, AlertType.Warning);
                            return View(model);
                        }
                }
            }
            model.Passport = passportStringOld;
            return View(model);
        }

        public ActionResult EditClass(string concat)
        {
            var eachItem = concat.Split(',').ToList();
            var classs = ServiceLocator.Get<ClassDto>();
            classs.Id = int.Parse(eachItem[0].ToString());
            classs.ClassName = (CurrentClass)Enum.Parse(typeof(CurrentClass), eachItem[1].ToString());
            classs.ClassLabel = eachItem[2];
            classs.SNo = byte.Parse(eachItem[3].ToString());

            ViewBag.className = _registrationService.CurrentsClassForEditingSubject(classs.ClassName);
            return PartialView(classs);
        }
        [HttpPost]
        public JsonResult EditClass(ClassDto classModel)
        {
            if (string.IsNullOrWhiteSpace(classModel.ClassLabel))
            {
                classModel.ClassLabel = classModel.ClassName.ToString();
            }
            var status = _registrationService.UpdateClass(classModel);
            if (status)
            {
                UnitOfWork.Commit();
                return Json(true, JsonRequestBehavior.AllowGet);
            }
            return Json(false, JsonRequestBehavior.DenyGet);
        }

        public ActionResult DeleteClass(int id)
        {
            var status = _registrationService.DeleteClass(id);
            if (status)
            {
                UnitOfWork.Commit();
                return Json(true, JsonRequestBehavior.AllowGet);
            }
            return Json(false, JsonRequestBehavior.DenyGet);
        }

        public ActionResult EditClassArm(string concat)
        {
            var eachItem = concat.Split(',').ToList();
            var classsArm = ServiceLocator.Get<ClassArmDto>();

            classsArm.Id = byte.Parse(eachItem[0]);
            classsArm.ArmName = (ArmName)Enum.Parse(typeof(ArmName), eachItem[1]);
            classsArm.SNo = byte.Parse(eachItem[2]);
            classsArm.ClassId = byte.Parse(eachItem[3]);
            ViewBag.classLabbel = eachItem[4];
            ViewBag.armName = _registrationService.ClasssArm(classsArm.ArmName);
            return PartialView(classsArm);
        }

        public ActionResult DeleteClassArm(short id)
        {
            var status = _registrationService.DeleteClassArm(id);
            if (status)
            {
                UnitOfWork.Commit();
                return Json(true, JsonRequestBehavior.AllowGet);
            }
            return Json(false, JsonRequestBehavior.DenyGet);
        }

        [HttpPost]
        public JsonResult EditClassArm(ClassArmDto classArmModel)
        {
            var status = _registrationService.UpdateClassArm(classArmModel);
            if (status)
            {
                UnitOfWork.Commit();
                return Json(true, JsonRequestBehavior.AllowGet);
            }
            return Json(false, JsonRequestBehavior.DenyGet);
        }

        public ActionResult SessionAndTermInitialisationIndex()
        {
            var list = _registrationService.GetSessionsAndTerm();
            if (list.Sessions == null)
            {
                return View(new List<AcademicSessionDto>());
            }
            ViewBag.currentTerm = list.CurrentTerm;
            return View(list.Sessions);
        }
        public ActionResult CreateAcademicSession()
        {
            return PartialView();
        }
        [HttpPost]
        public ActionResult CreateAcademicSession(AcademicSessionDto model)
        {
            if (ModelState.IsValid)
            {
                if (model.Session.Length != 9)
                {
                    ShowMessage("Failed! Value must be 9 character length.", AlertType.Danger);
                    ViewBag.session = _registrationService.TimeStatus(model.Status);
                    return View(model);
                }
                var status = _registrationService.CreateAcademicSession(model);
                if (!status) { ShowMessage("Failed! The academic session you entered has already been used.", AlertType.Warning); }
                else
                {
                    ShowMessage("The Academic Session has successfully been created.", AlertType.Success);
                    //UnitOfWork.SaveChanges();
                }
                return RedirectToAction("SessionAndTermInitialisationIndex", model);
            }
            ViewBag.session = _registrationService.TimeStatus(model.Status);
            return View(model);
        }

        public ActionResult EditSession(string concatValues)
        {
            if (string.IsNullOrEmpty(concatValues))
            {
                return View(new AcademicSessionDto());
            }
            var eachValue = concatValues.Split(',');
            TimeStatus status;
            if (eachValue[2] == TimeStatus.Current.ToString())
            {
                status = TimeStatus.Current;
            }
            else
            {
                status = TimeStatus.Previous;
            }
            ViewBag.session = _registrationService.TimeStatus(status);
            var model = new AcademicSessionDto
            {
                Id = byte.Parse(eachValue[0]),
                Session = eachValue[1],
                Status = status,
            };
            return PartialView(model);
        }

        [HttpPost]
        public ActionResult EditSession(AcademicSessionDto model)
        {
            if (ModelState.IsValid)
            {
                if (model.Session.Length != 9)
                {
                    ShowMessage("Failed! Value for academic session must be 9 character length.", AlertType.Danger);
                    ViewBag.session = _registrationService.TimeStatus(model.Status);
                    return View(model);
                }

                var status = _registrationService.UpdateAcademicSessionInfo(model);
                if (!status)
                { ShowMessage("Failed! The academic session you entered has already been used.", AlertType.Warning); }
                else
                {
                    UnitOfWork.Commit();
                    ShowMessage("Update Successful", AlertType.Success);
                    return RedirectToAction("SessionAndTermInitialisationIndex");
                }
            }
            ViewBag.session = _registrationService.TimeStatus(model.Status);
            return View(model);
        }
        private static bool CheckForInternetConnection()
        {
            Ping myPing = new Ping();
            String host = "google.com";
            byte[] buffer = new byte[32];
            int timeout = 1000;
            PingOptions pingOptions = new PingOptions();
            try
            {
                PingReply reply = myPing.Send(host, timeout, buffer, pingOptions);
                if (reply != null && reply.Status == IPStatus.Success)
                {
                    return true;
                }
                else
                {
                    try
                    {
                        using (var client = new WebClient())
                        using (var stream = client.OpenRead("http://www.google.com"))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    using (var client = new WebClient())
                    using (var stream = client.OpenRead("http://www.google.com"))
                    {
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            }

        }

        private string TheRightNaitsHostName()
        {
            var rightHostName = GetTheRightNaitsHostName("ourschoolonline.ng");
            return rightHostName;
        }

        private string GetTheRightNaitsHostName(string hostName)
        {
            try
            {
                using (var client = new WebClient())
                using (var stream = client.OpenRead("http://www." + hostName))
                {
                    var completeHostName = "http://www." + hostName;
                    return completeHostName;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    using (var client = new WebClient())
                    using (var stream = client.OpenRead("https://www." + hostName))
                    {
                        var completeHostName = "https://www." + hostName;
                        return completeHostName;
                    }
                }
                catch (Exception ex2)
                {
                    try
                    {
                        using (var client = new WebClient())
                        using (var stream = client.OpenRead("http://www." + hostName))
                        {
                            var completeHostName = "http://www." + hostName;
                            return completeHostName;
                        }
                    }
                    catch (Exception ex4)
                    {
                        try
                        {
                            using (var client = new WebClient())
                            using (var stream = client.OpenRead("https://www." + hostName))
                            {
                                var completeHostName = "https://www." + hostName;
                                return completeHostName;
                            }
                        }
                        catch
                        {
                            return "";
                        }
                    }
                }
            }
        }

        public ActionResult AssessmentEnviromentalPreparation()
        {
            var securityService = ServiceLocator.Get<ISecurityService>();

            AccessVerifyReturnValuesAndMoreDto bStatus = securityService.B();//B means VerifyAccess
            if (bStatus.AccessVerifyReturnValues != null)
            {
                ShowMessage(bStatus.AccessVerifyReturnValues.Value1, AlertType.Danger);
                return RedirectToAction(bStatus.AccessVerifyReturnValues.Value2,
                    bStatus.AccessVerifyReturnValues.Value3,
                    new { Area = bStatus.AccessVerifyReturnValues.Value4 });
            }

            if (bStatus.LoginReturnStatus.StatusCode == "Failure")
            {
                ShowMessage(bStatus.LoginReturnStatus.ReturnMessage, AlertType.Danger);
                return View("StudentAccessmentSystemInitialPreparation");
            }
            UnitOfWork.Commit();
            if (bStatus.LoginReturnStatus.StatusCode == "1")
            {
                ShowMessage(bStatus.LoginReturnStatus.ReturnMessage, AlertType.Danger);
                return View("StudentAccessmentSystemInitialPreparation");
            }
            if (bStatus.LoginReturnStatus.StatusCode == "2")
            {
                var implementationStatus = _registrationService.GetSystemImplementationStatus();//Can be null at this point
                if (implementationStatus == "0" || FreePlugIn.Decrypt(implementationStatus, true).Contains(":0"))
                {
                    UnitOfWork.Rollback();
                    var status = _registrationService.PrepareAssessmentEnvironmentForTheWholeStudents(null);
                    if (!string.IsNullOrEmpty(status.Message))
                    {
                        ViewBag.msg = status.Message;
                        if (status.Message == "Success")
                        {
                            var total = status.TotalStudentListToPdf?.Count;
                            if (total != null && total <= 0)
                            {
                                var students = status.TotalStudentListToPdf;
                                var studentList = students.OrderBy(d => d.CurrentClass).ThenBy(f => f.Arm);
                                var pv = securityService.GetPv();
                                Ty(studentList, (int)total, pv, SystemImplementationType.PerTerm);//Ty maens RenderStudentsListToPdfAndMail
                                UnitOfWork.Commit();
                            }
                            else
                            {
                                return Json("Sorry! No student in the selected class-arm", JsonRequestBehavior.AllowGet);
                            }
                        }
                        else
                        {
                            UnitOfWork.Commit();
                            ShowMessage(status.Message, AlertType.Warning);
                        }
                        return View("StudentAccessmentSystemInitialPreparation");
                    }
                }
                else
                {
                    var checkStatus = _registrationService.GetAppropriatePaymentStatus();
                    if (checkStatus == 0)
                    {
                        UnitOfWork.Rollback();
                        var status = _registrationService.PrepareAssessmentEnvironmentForTheWholeStudents(null);

                        if (!string.IsNullOrEmpty(status.Message))
                        {
                            ViewBag.msg = status.Message;
                            if (status.Message == "Success")
                            {
                                var total = status.TotalStudentListToPdf?.Count;
                                if (total != null && total > 0)
                                {
                                    try
                                    {
                                        UnitOfWork.Commit();
                                    }
                                    catch (Exception ex)
                                    {
                                        UnitOfWork.Rollback();
                                        ShowMessage("Failed! Unable to complete the process.", AlertType.Warning);
                                    }
                                }
                                else
                                {
                                    return Json("Sorry! No student in the selected class-arm", JsonRequestBehavior.AllowGet);
                                }
                            }
                            else
                            {
                                UnitOfWork.Commit();
                                ShowMessage(status.Message, AlertType.Warning);
                            }
                            return View("StudentAccessmentSystemInitialPreparation");
                        }
                    }
                    else
                    {
                        return Json("Sorry! You need to pay for the extra " + checkStatus + " students you have recently added.",
                            JsonRequestBehavior.AllowGet);
                    }              
                }
            }
            ShowMessage(bStatus.LoginReturnStatus.ReturnMessage, AlertType.Warning);
            return View("StudentAccessmentSystemInitialPreparation");
        }

        public ActionResult Resolve()
        {
            //ServiceLocator.Get<IRegistrationServise>().CorrectStudentAcademicProgressHistoryIssues2();
            ServiceLocator.Get<IRegistrationServise>().BringBackAllPreviousStudentsInThisSchool();


            //_registrationService.DistributeStudentEvenly();
            //_registrationService.CorrectStudentInitialRegistrationStatus();
            //_registrationService.CorrectStudentAdmisionTermId();

            //var currentStudentTotalPayment = 5.2;
            //var hisFees = 5.23;

            //if (Math.Abs(currentStudentTotalPayment - hisFees) < 0.04)
            //{

            //}
            //if (Math.Abs(hisFees - currentStudentTotalPayment) < 0.04)
            //{

            //}

            //_registrationService.CorrectStudentAcademicProgressHistory();
            //_registrationService.CorrectStudentClassCategory();
            // _registrationService.CorrectStudentAcademicProgressHistory();
            //_registrationService.ClearCurrentTermAcademicProgressHistoryForPreviousStudent();

            //_registrationService.CorrectPlayClassExtensionProblem();

            //_registrationService.UpdateStartTermId();
            //_registrationService.CorrectClassLevel();
            // _registrationService.ClearCurrentAssessmentPreparation();
            // _registrationService.CorrectPreviosResult();
            // _registrationService.CorrectCreatedDate();

            ShowMessage("The issue has successfully been resolved.", AlertType.Success);
            return RedirectToAction("Index");
        }
        public ActionResult CheckSoftwareValidity()
        {
            var securityService = ServiceLocator.Get<ISecurityService>();

            try
            {
                var status = securityService.B();//B means VerifyAccess
                ShowMessage(status.LoginReturnStatus.ReturnMessage, AlertType.Warning);
            }
            catch (Exception ex)
            {
                return Json(true, JsonRequestBehavior.AllowGet);
            }
            return Json(false, JsonRequestBehavior.AllowGet);
        }
        public ActionResult RunPromotionValidation()
        {
            var status = _registrationService.StudentsPromotionValidations();
            return Json(status, JsonRequestBehavior.AllowGet);
        }

        public ActionResult ConfirmPromotionRun()
        {
            var status = _registrationService.ConfirmStudentsPromotion();
            if (status == "Succeeded")
            {
                UnitOfWork.Commit();
                return Json("Succeeded", JsonRequestBehavior.AllowGet);
            }
            else if (status == "Failed")
            {
                return Json("Some form of serious validation error has occured", JsonRequestBehavior.AllowGet);
            }
            return Json(status, JsonRequestBehavior.AllowGet);//StudentsAutomaticPromotion
        }


        public ActionResult ConfirmAutomaticPromotionRun()
        {
            _registrationService.StudentsAutomaticPromotion();
            return Json("Succeeded", JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult DeleteAcademicSession(byte id)
        {
            var status = _registrationService.DeleteAcademicSession(id);
            if (status)
            {
                UnitOfWork.Commit();
                return Json(true, JsonRequestBehavior.AllowGet);
            }
            return Json(false, JsonRequestBehavior.AllowGet);
        }
        public ActionResult SearchStudent(string searchParameter)
        {
            if (!string.IsNullOrWhiteSpace(searchParameter))
            {
                var studentModel = _registrationService.GetStudentsByParameter(searchParameter);
                if (studentModel != null && studentModel.Any())
                {
                    return PartialView(studentModel);
                }
            }
            return PartialView(new List<StudentDetailsDto>());
        }

        public ActionResult RenderStudents(string concatParameters)
        {
            if (!string.IsNullOrWhiteSpace(concatParameters))
            {
                var studentModel = _registrationService.GetArmStudents(concatParameters);

                var eachValue = concatParameters.Split(',').ToList();
                ViewBag.classAndArm = eachValue[1] + (ArmName)int.Parse(eachValue[2]) + " Students. Total: " + studentModel.Count();
                if (studentModel != null && studentModel.Any())
                {
                    return PartialView("SearchStudent", studentModel);
                }
            }
            ViewBag.msg = "No student in the selected class-arm.";
            return PartialView("SearchStudent", new List<StudentDetailsDto>());
        }
        public ActionResult GenderHandlers(string concatParameters)
        {
            if (!string.IsNullOrWhiteSpace(concatParameters))
            {
                var studentModel = _registrationService.GenderHandler(concatParameters);
                if (studentModel)
                {
                    UnitOfWork.Commit();
                    return Json(true, JsonRequestBehavior.AllowGet);
                }
            }
            return Json(false, JsonRequestBehavior.AllowGet);
        }
        public ActionResult EnableUser(int id)
        {
            var status = _registrationService.EnableUserById(id);
            if (status)
            {
                UnitOfWork.Commit();
                ShowMessage("The user has successfully been Enabled.", AlertType.Success);
                var list = _registrationService.GetAllTeachers();
                return PartialView("RegisteredTeacher", list);
            }
            ShowMessage("Failed! We were unable to complete the request.", AlertType.Success);
            return PartialView("RegisteredTeacher", new List<EmployeeDto>());
        }
        public ActionResult DisableUser(int id)
        {
            var status = _registrationService.DisableUserById(id);
            if (status)
            {
                UnitOfWork.Commit();
                ShowMessage("The user has successfully been disabled.", AlertType.Success);
                var list = _registrationService.GetAllTeachers();
                return PartialView("RegisteredTeacher", list);
            }
            ShowMessage("Failed! We were unable to complete the request.", AlertType.Success);
            return PartialView("RegisteredTeacher");
        }

        public ActionResult EditSchoolLevelCategoriesLabel(string concatParam)
        {
            var eachParameter = concatParam.Split(',').ToList();
            var schoolLevelCategoriesLabelModel = ServiceLocator.Get<SchoolLevelCategoriesLabelDto>();
            schoolLevelCategoriesLabelModel.PlayClassLabel = eachParameter[0].Replace(@"&amp;", "&");
            schoolLevelCategoriesLabelModel.PreNurseryLabel = eachParameter[1].Replace(@"&amp;", "&");
            schoolLevelCategoriesLabelModel.NurseryLabel = eachParameter[2].Replace(@"&amp;", "&");

            //var labelCode = Uri.UnescapeDataString(eachParameter[2]);
            //var labekcode2= HttpUtility.UrlDecode(eachParameter[2]);

            schoolLevelCategoriesLabelModel.PrimaryLabel = eachParameter[3].Replace(@"&amp;", "&");
            schoolLevelCategoriesLabelModel.JssLabel = eachParameter[4].Replace(@"&amp;", "&");
            schoolLevelCategoriesLabelModel.SsLabel = eachParameter[5].Replace(@"&amp;", "&");
            
            //schoolLevelCategoriesLabelModel.CustomLevelLabel = eachParameter[6];
            schoolLevelCategoriesLabelModel.ElectiveSubjectLabel = eachParameter[6].Replace(@"&amp;", "&");
            return PartialView(schoolLevelCategoriesLabelModel);
        }

        [HttpPost]
        public JsonResult EditSchoolLevelCategoriesLabel(SchoolLevelCategoriesLabelDto model)
        {
            if (ModelState.IsValid)
            {
                var status = _registrationService.UpdateSchoolLabel(model);
                if (status)
                {
                    UnitOfWork.Commit();
                    return Json(true, JsonRequestBehavior.AllowGet);
                }
            }
            return Json(false, JsonRequestBehavior.AllowGet);
        }

        public ActionResult SsSubjectEnvironment(string ssSubjectLevel)
        {
            ViewBag.ssSubjectLevel = ssSubjectLevel;
            return PartialView();
        }

        public ActionResult ReArrangeClasses(int schoolInfoId)
        {
            var classList = _registrationService.GetClassesInSchoolBySchoolId(schoolInfoId);
            return View(classList);
        }

        public JsonResult SubmitClassReArrangement(List<ClassDto> model)
        {
            var status = _registrationService.UpdateClassReArrangement(model);
            return Json(true, JsonRequestBehavior.AllowGet);
        }
        public ActionResult SearchTeacher(string userName)
        {
            if (!string.IsNullOrEmpty(userName))
            {
                var teacher = _registrationService.GetTeacherByUserName(userName);
                ViewBag.msg = "Failed! No record match the entered Username";
                return PartialView("RegisteredTeacher", teacher);
            }
            return PartialView("RegisteredTeacher", new List<EmployeeDto>());
        }

        public ActionResult SearchSubjectTeacher(string userName)
        {
            if (!string.IsNullOrEmpty(userName))
            {
                var teacher = _registrationService.GetSubjectTeacherByUserName(userName);
                ViewBag.msg = "No Username match the name you have just typed.";
                return PartialView("SubjectTeachers", teacher);
            }
            ViewBag.msg = "Failed!";
            return PartialView("SubjectTeachers", new List<TeacherSubjectArmAssignmentDto>());
        }
        public ActionResult SearchFormMaster(string userName)
        {
            if (!string.IsNullOrEmpty(userName))
            {
                var teacher = _registrationService.GetFormMasterByUserName(userName);
                ViewBag.msg = "No Username match the name you have just typed.";
                return PartialView("FormTeachers", teacher);
            }
            ViewBag.msg = "Failed!";
            return PartialView("FormTeachers", new List<TeacherClassArmAssignmentDto>());
        }

        public ActionResult SchoolDefaultRegistration(string schoolName, string token, string key)///SchoolDefualtRegistration
        {
            var checks = _registrationService.CreateSchool(schoolName, token, key);
            _registrationService.ProcessAnonymousUserDefault4MonthsVailidity();
            if (checks)
            {
                UnitOfWork.SaveChanges();
            }
            return RedirectToAction("SchoolRegistration");
        }

        public ActionResult TotalAttendance()
        {
            return View();
        }

        [HttpPost]
        public ActionResult TotalAttendance(int noOfDaysInThisTerm)
        {
            var status = _registrationService.CreateTotalAttendance(noOfDaysInThisTerm);
            if (status)
            {
                ShowMessage("The next term date has successfully been submited.", AlertType.Success);
                UnitOfWork.Commit();
            }
            else
            {
                ShowMessage("Continuous assessment intitial background preparation needs to be done before this.", AlertType.Warning);
            }
            return View();
        }

        public JsonResult ViewTotalAttendance()
        {
            var attendance = _registrationService.GetCurrentTotalAttendance();
            if (attendance != null)
            {
                return Json("The Total Attendance for this term is: " + attendance, JsonRequestBehavior.AllowGet);
            }
            return Json("Attendance not yet set.", JsonRequestBehavior.DenyGet);
        }

        public ActionResult ReligionSubjectsEnvironment()
        {
            return PartialView();
        }

        public ActionResult AllSubjectIndex()
        {
            var sujects = _registrationService.GetSubjectForDropDown();
            sujects.Add(new SelectListItem
            {
                Text = "AllSubjects",
                Value = "AllSubjects"
            });
            ViewBag.subjects = sujects;
            return View();
        }

        //public ActionResult SearchSubject()
        //{

        //    return PartialView();
        //}

        public ActionResult RenderListOfLevelsOfferingThisSuject(string subjectName)
        {
            var list = _registrationService.GetLevelsThatOfferThisSubject(subjectName);
            return PartialView(list);
        }

        public ActionResult SearchLevelsOfferingThisSuject(string searchParameter)
        {
            var list = _registrationService.SearchLevelsThatOfferThisSubject(searchParameter);
            if (!list.Any())
            {
                ViewBag.msg = "No Subject matched with the text you have just typed.";
            }
            return PartialView("RenderListOfLevelsOfferingThisSuject", list);
        }

        public ActionResult TransferOfAssignment(string directionStatus)
        {
            var stuff = _registrationService.GetTeachersClassesAndSujects();
            ViewBag.subjects = stuff.SubjectsName;
            ViewBag.classes = stuff.ClassName;

            if (!string.IsNullOrEmpty(directionStatus))
            {
                var teacherDropdown = _registrationService.GetTeachersForDropdown();

                ViewBag.proposedTeacher = teacherDropdown;
                teacherDropdown.Add(new SelectListItem
                {
                    Text = "Sample",
                    Value = "Sample"

                });
                ViewBag.currentTeacher = teacherDropdown;
                directionStatus = directionStatus.Trim();
                ViewBag.directionStatus = directionStatus;
            }
            else
            {
                ViewBag.directionStatus = "Null";
            }
            return View();
        }

        public ActionResult TeacherAssignedSubjects(string selectedTeacherId, string directionStatus)
        {
            var list = _registrationService.GetTeacherAssignedSubjectsByTeacherId(selectedTeacherId);
            ViewBag.directionStatus = directionStatus;
            return PartialView(list);
        }

        public ActionResult TeacherAssignedArms(string selectedTeacherId, string directionStatus)
        {
            var list = _registrationService.GetTeacherAssignedArmsByTeacherId(selectedTeacherId);
            ViewBag.directionStatus = directionStatus;
            return PartialView(list);
        }

        public ActionResult UnAssignSubjectSelections(List<SubjectIdsBto> subjectIds)
        {
            var status = _registrationService.TransferSubjectTeacherSubjectAssignment(subjectIds);
            if (status)
            {
                UnitOfWork.Commit();
                return Json("true", JsonRequestBehavior.AllowGet);
            }
            return Json("false", JsonRequestBehavior.AllowGet);
        }

        public ActionResult UnAssignArmSelections(List<SubjectIdsBto> subjectIds)
        {
            var status = _registrationService.TransferArmTeacherArmAssignment(subjectIds);
            if (status)
            {
                UnitOfWork.Commit();
                return Json("true", JsonRequestBehavior.AllowGet);
            }
            return Json("false", JsonRequestBehavior.AllowGet);
        }
        public ActionResult RenderSelectArmsForSubjectAssignment(string concatParameters)
        {
            var lists = concatParameters.Split(',').ToList();
            if (lists[0].Trim() == "default")
            {
                ViewBag.msg = "Please re-select again.";
                return PartialView(new List<ClassArmDto>());
            }
            var model = _registrationService.GetArmsByClassIdAgainstSelectSubject(concatParameters);
            ViewBag.selectedId = concatParameters;
            if (!string.IsNullOrEmpty(model.Message))
            {
                ViewBag.msg = model.Message;
                return PartialView(new List<ClassArmDto>());
            }
            return PartialView(model.Arms);
        }
        public ActionResult RenderSubjectsForSelectClasss(string selectedClassId)
        {
            var myDropdown = _registrationService.GetClassSubjectsForDropDown(selectedClassId);
            if (!myDropdown.Any())
            {
                myDropdown.Add(new SelectListItem { Text = "No Subject or all Subjects have been assigned." });
            }
            else if (myDropdown.Count == 1)
            {
                myDropdown.Add(new SelectListItem { Text = "Default", Value = "default" });
            }
            ViewBag.subjects = myDropdown;
            return PartialView();
        }
        public ActionResult SubjectStudentAssignmentIndex()
        {
            ViewBag.electiveSubjectsDropdown = _registrationService.ElectiveSubjectDropDown();
            ViewBag.classLabelDropdown = _registrationService.ClassDropDown();
            return View();
        }
        //public ActionResult RenderSubjectsDropdown(int selectedClassId)
        //{
        //    ViewBag.subjects= _registrationService.SubjectDropDown(selectedClassId);
        //    return PartialView();
        //}
        public ActionResult RenderArmsDropdown(int selectedClassId, int subjectIdd)
        {
            ViewBag.subjectId = subjectIdd;
            var arm = _registrationService.ArmDropDown(selectedClassId);
            if (arm != null && arm.Count == 1)
            {
                arm.Add(new SelectListItem { Text = "Default", Value = "0" });
                ViewBag.arms = arm;
            }
            else if ((arm != null && arm.Count == 0))
            {
                arm.Add(new SelectListItem { Text = "The selected Class does not contain Arm" });
                ViewBag.arms = arm;
            }
            else if (arm == null)
            {
                var returnArm = new List<SelectListItem> { new SelectListItem { Text = "The selected Class does not contain Arm" } };
                ViewBag.arms = returnArm;
            }
            else
            {
                ViewBag.arms = arm;
            }
            return PartialView();
        }

        public ActionResult RenderStudentInTheSelectedArm(int selectedArmId, int subjectIdd)
        {
            ViewBag.selectedArmId = selectedArmId;
            //ViewBag.subjectId = subjectIdd;
            if (selectedArmId == 0)
            {
                return PartialView(new List<StudentDetailsDto>());
            }
            var students = _registrationService.GetStudentForAnArm2Nd(selectedArmId, subjectIdd);
            ViewBag.subjectStudentAssignment = students.AssignedStudentIds;
            return PartialView(students.StudentsList);
        }

        public ActionResult RenderCategoryStudentsInTheSelectedArm(byte catStatus = 0, int armId = 0, int subjectId = 0)
        {
            ViewBag.selectedArmId = armId;
            var result = _registrationService.GetCategoryStudentsInTheSelectedArm(catStatus, armId, subjectId);
            ViewBag.subjectStudentAssignment = result.AssignedStudentIds;
            if (result.StudentsList.Any())
            {
                return PartialView(result.StudentsList.ToList());
            }
            ViewBag.msg = "No student in the selected category in the selected class-arm.";
            return PartialView(new List<StudentDetailsDto>());
        }

        public ActionResult ArmStudentsOrStudentsInCategorySubjectAssignment(List<StudentIdsDto> studentIds)
        {
            var test = _registrationService.AssignSubjectToArmStudentsOrStudentsInCategory(studentIds);
            UnitOfWork.Commit();
            return Json(test, JsonRequestBehavior.AllowGet);
            // return Json("Failed! Un-able to complete the process.", JsonRequestBehavior.AllowGet);
        }

        public ActionResult ArmStudentsOrStudentsInCategorySubjectUnAssignment(List<StudentIdsDto> studentIds)
        {
            var test = _registrationService.UnAssignSubjectToArmStudentsOrStudentsInCategory(studentIds);
            if (test)
            {
                UnitOfWork.Commit();
                return Json("succeed", JsonRequestBehavior.AllowGet);
            }
            return Json("Failed! Un-able to complete the process.", JsonRequestBehavior.AllowGet);
        }

        public ActionResult SearchSubjectTeacher2Nd(string parameter)
        {
            if (!string.IsNullOrEmpty(parameter))
            {
                var teacher = _registrationService.GetSubjectTeacherByUserName(parameter);
                ViewBag.msg = "No Username match the name you have just typed.";
                return PartialView(teacher);
            }
            ViewBag.msg = "Failed!";
            return PartialView(new List<TeacherSubjectArmAssignmentDto>());
        }

        public ActionResult SearchArmTeacher2Nd(string parameter)
        {
            if (!string.IsNullOrEmpty(parameter))
            {
                var teacher = _registrationService.GetFormMasterByUserName(parameter);
                ViewBag.msg = "No Username match the name you have just typed.";
                return PartialView("TeacherAssignedArms", teacher);
            }
            ViewBag.msg = "Failed!";
            return PartialView("TeacherAssignedArms", new List<TeacherClassArmAssignmentDto>());
        }

        public ActionResult LoadStudentSubjectSelection()
        {
            var dropDown = _registrationService.AccessStatu();
            foreach (var each in dropDown)
            {
                if (each.Text == "Active")
                {
                    each.Text = "Enable";
                }
            }
            ViewBag.currentAccessStatus = dropDown;
            return PartialView();
        }

        public ActionResult EnaleAndDisableStudentSubjectSelection(byte? selectionId)
        {
            if (selectionId == null)
            {
                return Json(false, JsonRequestBehavior.AllowGet);
            }
            //var registrationServise = ServiceLocator.Get<IRegistrationServise>();
            var status = _registrationService.ChangeStudentsElectiveSelectionStatus(selectionId);
            if (status)
            {
                UnitOfWork.Commit();
                return Json(true, JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(false, JsonRequestBehavior.AllowGet);
            }
        }
        public ActionResult EnableStudentAccount(string userId)
        {
            var status = _registrationService.EnableUserById(userId);
            if (status)
            {
                UnitOfWork.Commit();
                ShowMessage("The user has successfully been Enabled.", AlertType.Success);
                var list = _registrationService.GetRecentlySavedStudents();
                return PartialView("SearchStudent", list);
            }
            ShowMessage("Failed! We were unable to complete the request.", AlertType.Success);
            return PartialView("SearchStudent", new List<StudentDetailsDto>());
        }
        public ActionResult DisableStudentAccount(string userId)
        {
            var status = _registrationService.DisableUserById(userId);
            if (status)
            {
                UnitOfWork.Commit();
                ShowMessage("The user has successfully been disabled.", AlertType.Success);
                var list = _registrationService.GetRecentlySavedStudents();
                return PartialView("SearchStudent", list);
            }
            ShowMessage("Failed! We were unable to complete the request.", AlertType.Success);
            return PartialView("SearchStudent", new List<StudentDetailsDto>());
        }

        public ActionResult ChangeStudentPassword(string studentFullName, string userId)
        {
            ViewBag.userId = userId;
            ViewBag.studentFullName = studentFullName;
            return PartialView();
        }
        [HttpPost]
        public ActionResult ChangeStudentPassword(StudentPasswordModelDto model)
        {
            if (string.IsNullOrWhiteSpace(model.Password) || string.IsNullOrWhiteSpace(model.ConfirmPassword))
            {
                return Json(0, JsonRequestBehavior.AllowGet);       //Password and Confirm Password field are required         
            }
            else if (model.Password != model.ConfirmPassword)
            {
                return Json(1, JsonRequestBehavior.AllowGet);// //Password and Confirm Password mis-match         
            }
            else if (model.Password.Trim().Length < 6)
            {
                return Json(2, JsonRequestBehavior.AllowGet);
            }
            var status = _registrationService.ChangeStudentPassword(model);
            if (status)
            {
                UnitOfWork.Commit();
                return Json(3, JsonRequestBehavior.AllowGet); //Password changed successfully
            }
            return Json(4, JsonRequestBehavior.AllowGet);//An unable to complete the process
        }

        public ActionResult StudentResultIndex()
        {
            var session = _registrationService.GetSessionsDropdown();
            if (session != null && session.Count > 0)
            {
                if (session.Count == 1)
                {
                    var sessionHolder = session.First();
                    session.Add(new SelectListItem { Text = "Select", Value = "-1" });
                    session.RemoveAt(0);
                    session.Add(sessionHolder);
                }
                var term = _registrationService.GetTermsEnumTypeDropdownForSelectedSession(int.Parse(session.First().Value));
                if (term != null && term.Count == 1)
                {
                    var termHolder = term.First();
                    term.Add(new SelectListItem { Text = "Select", Value = "-1" });
                    term.RemoveAt(0);
                    term.Add(termHolder);
                }
                ViewBag.term = term;
            }
            ViewBag.session = session;
            return View();
        }

        public ActionResult TermDropdownForSelectedSession(int selectedSessionId)
        {
            var term = _registrationService.GetTermsEnumTypeDropdownForSelectedSession(selectedSessionId);
            if (term != null && term.Count == 1)
            {
                var termHolder = term.First();
                term.Add(new SelectListItem { Text = "Select", Value = "-1" });
                term.RemoveAt(0);
                term.Add(termHolder);
            }
            ViewBag.term = term;
            return PartialView();
        }

        public ActionResult SelectedStudentYearlyReportSheet(string studentId, int selectedSessionId)
        {
            //var registrationService = ServiceLocator.Get<IRegistrationServise>();

            var teachersAndOthersService = ServiceLocator.Get<ITeachersAndOthersService>();
            var studentsReports = teachersAndOthersService.GetYearlyAssessmentForSelectedStudent(studentId, selectedSessionId);

            if (studentsReports?.StudentsAccessment != null && studentsReports.StudentsAccessment.Any())
            {
                var model = _registrationService.GetSchoolInfo();
                ViewBag.emailAddress = model.EmailAddress;
                ViewBag.PoAddress = model.PoAddress;
                ViewBag.SchoolAddress = model.SchoolAddress;
                ViewBag.SchoolName = model.SchoolName;
                ViewBag.Telephone = model.Telephone;
                ViewBag.Website = model.Website;
                ViewBag.formMaster = studentsReports.ArmFormMaster;
                ViewBag.studentsAttendance = studentsReports.StudentsAttendance;

                ViewBag.MSG = studentsReports.ErrorMsg;

                ViewBag.armName = studentsReports.StudentsAccessment.First().ClassArm.ArmName;
                ViewBag.className = studentsReports.StudentsAccessment.First().ClassArm.Classs.ClassName;
                ViewBag.armId = studentsReports.StudentsAccessment.First().ClassArmId;

                return PartialView(studentsReports.StudentsAccessment);
            }
            ViewBag.MSG = studentsReports?.ErrorMsg;
            return PartialView(new List<StudentsAccessmentDto>());
        }

        public ActionResult SelectedStudentTermlyReportSheet(string studentId, int sessionId, TermStatus termStatus)
        {
            var teachersAndOthersService = ServiceLocator.Get<ITeachersAndOthersService>();
            var studentsReports = teachersAndOthersService.GetTermlyAssessmentForSelectedStudent(studentId, sessionId, termStatus);

            if (studentsReports == null)
            {
                ViewBag.MSG = "Sorry! no student in the selected Arm.";
                return PartialView(new List<StudentsAccessmentDto>());
            }
            if (studentsReports.StatusCode != null)
            {
                if (studentsReports.StatusCode == 0)
                {
                    ViewBag.MSG = "Failed! Either wrong Student's Admission number or no result is available for the selected SESSION and TERM.";
                }
                else if (studentsReports.StatusCode == 1)
                {
                    ViewBag.MSG = "Failed! Environment has not been prepared for result processing yet; contact Registration Officer.";
                }
                else if (studentsReports.StatusCode == 2)
                {
                    ViewBag.MSG = "Failed; because Affective and Psychomotor has not been entered for any Student in that 'class arm' during the selectted session and term.";
                }
                return PartialView(new List<StudentsAccessmentDto>());
            }

            ViewBag.studentId = studentId;
            ViewBag.caSetting = studentsReports.GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting.CasSetting;

            if (studentsReports?.GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting.StudentsAccessment != null &&
                studentsReports.GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting.StudentsAccessment.Any())
            {
                var model = teachersAndOthersService.GetSchoolModelForSchoolRegistrationOfficer();

                ViewBag.emailAddress = model.EmailAddress;
                ViewBag.PoAddress = model.PoAddress;
                ViewBag.SchoolAddress = model.SchoolAddress;
                ViewBag.SchoolName = model.SchoolName;
                ViewBag.Telephone = model.Telephone;
                ViewBag.Website = model.Website;
                ViewBag.schoolLogoString = model.SchoolLogoString;
                ViewBag.motto = model.Motto;
                ViewBag.studentsAttendance = studentsReports.GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting.StudentsAttendance;
                ViewBag.caSetting = studentsReports.GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting.CasSetting;
                if (studentsReports.GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting.AffectiveAndPsychomotorAssessment != null && studentsReports.GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting.AffectiveAndPsychomotorAssessment.Any())
                {

                    ViewBag.schoolHead = studentsReports.GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting.SchoolHeadInfo;
                    ViewBag.armFormMaster = studentsReports.GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting.AffectiveAndPsychomotorAssessment.First().FormMaster;
                    ViewBag.affectiveAndPsychomotorAssessment = studentsReports.GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting.AffectiveAndPsychomotorAssessment;
                    ViewBag.totalAttendance = studentsReports.GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting.AffectiveAndPsychomotorAssessment.First().YearlyAndTermlyAssessmentStatus.TotalAttendance;
                }
            }
            var resultList =
                studentsReports.GeneralResultProcessingsToReportSheetXtractionForFormTeacherCasSetting
                    .StudentsAccessment.ToList();

            return PartialView(resultList);
        }

        public ActionResult SetSchoolHead()
        {
            var status = _registrationService.CreateSchoolHead();
            if (status == 0)
            {
                UnitOfWork.Commit();
            }
            return Json(status.ToString(), JsonRequestBehavior.AllowGet);
        }

        public ActionResult ViewSetSchoolHead()
        {
            var schoolHeadName = _registrationService.GetSchoolHeadNames();
            return Json(schoolHeadName, JsonRequestBehavior.AllowGet);
        }
        public ActionResult AssessmentCompletionDefaultedTeachers()
        {
            byte requestStatus = 1;
            var status = _registrationService.PrepareAssessmentEnvironmentForTheWholeStudents(requestStatus);

            if (status.UncompletedFormMastersReport == null || status.UncompletedFormMastersReport.Count == 0)
            {
                ViewBag.msg = status.Message;
            }
            return PartialView("AssessmentEnviromentalPreparation", status.UncompletedFormMastersReport);
        }
        public ActionResult CasIndex()
        {
            var casSetting = _registrationService.GetCurrentCasSettings();
            if (casSetting.NumberOfCasCategoyDropdown == null)
            {
                ShowMessage("Sorry! Other registrations first need to be done before this place.", AlertType.Warning);
            }
            ViewBag.studentStatus = casSetting;
            return View();
        }
        [HttpPost]
        public ActionResult SubmitCaSetting(CasSettingDto model)
        {
            if (ModelState.IsValid)
            {
                var status = _registrationService.SubmitCaCategorySetting(model);
                if (status == true)
                {
                    UnitOfWork.Commit();
                    ShowMessage("Your changes has successfully been saved.", AlertType.Success);
                }
                else
                {
                    ShowMessage("Failed! Other registration first need to be done before here.", AlertType.Danger);
                }
            }
            return RedirectToAction("CasIndex");
        }
        public ActionResult LoadSetPassMark()
        {
            var passMark = _registrationService.GetPassMark();
            ViewBag.passMark = passMark;
            return PartialView();
        }

        public ActionResult SubmitPassMark(byte passMark)
        {
            var status = _registrationService.SubmitPassMark(passMark);
            if (status)
            {
                UnitOfWork.SaveChanges();
                return Json(true, JsonRequestBehavior.AllowGet);
            }
            return Json(false, JsonRequestBehavior.AllowGet);
        }
        public ActionResult Dashboard()
        {
            var summary = _registrationService.GetSchoolSummaryForRegistrarDashboard();
            if (summary != null)
            {
                ViewBag.session = summary.Session;
                ViewBag.term = summary.Term;
                ViewBag.totalStudents = summary.TotalStudents;
                ViewBag.totalTeacher = summary.TotalTeachers;
                ViewBag.studentsAccessment = summary.StudentsAccessment;
                ViewBag.dashboardStudentsNumberAnalysis = summary.DashboardStudentsNumberAnalysis;
            }
            else
            {
                ViewBag.session = "Not yet set";
                ViewBag.term = "Not yet set";
                int total = 0;
                ViewBag.totalStudents = total;
                ViewBag.totalTeacher = 0;
                ViewBag.studentsAccessment = null;
                ShowMessage("You are welcome! Nothing to show yet.", AlertType.Success);
                ViewBag.dashboardStudentsNumberAnalysis = new List<DashboardStudentsNumberAnalysisDto>();
            }
            return View();
        }

        public ActionResult ChangeStudentSatusToPrevious(byte previousStatus, string id)
        {
            bool status = _registrationService.AlterStudentStatusToPrevious(previousStatus, id);
            return Json(status, JsonRequestBehavior.AllowGet);
        }

        public ActionResult AssessmentDeadline()
        {
            return View();
        }
        [HttpPost]
        public ActionResult AssessmentDeadline(DateTime deadline)
        {
            var status = _registrationService.SetAssessmentDeadline(deadline);
            if (status)
            {
                ShowMessage("The assessment deadline has successfully been created.", AlertType.Success);
                UnitOfWork.Commit();
            }
            else
            {
                ShowMessage("Continuous assessment intitial background preparation needs to be done before this.", AlertType.Warning);
            }
            return View();
        }
        [HttpPost]
        public JsonResult AssessementDeadline()
        {
            var status = _registrationService.GetAssessementDeadline();
            if (status != null)
            {
                var dateString = status.Value.Date.ToLongDateString();
                return Json("The entering of student assessment ends on: " + dateString, JsonRequestBehavior.AllowGet);
            }
            return Json("No set date yet.", JsonRequestBehavior.AllowGet);
        }

        public ActionResult ResultTypeMenu(int selectedOption)
        {
            var session = _registrationService.GetSessionsDropdown();
            if (session != null && session.Count > 0)
            {
                var term = _registrationService.GetTermsEnumTypeDropdownForSelectedSession(int.Parse(session.Last().Value));
                if (term != null)
                {
                    if (term.Count == 1)
                    {
                        var termHolder = term.First();
                        term.Add(new SelectListItem { Text = "Select", Value = "-1" });
                        term.RemoveAt(0);
                        term.Add(termHolder);
                    }
                    ViewBag.term = term.OrderByDescending(x => int.Parse(x.Value));
                }
                ViewBag.session = session.OrderByDescending(x => x.Value);
            }

            ViewBag.selectedOption = selectedOption;
            var stuff = _registrationService.StudentIndexClassAndArmDropDown();
            var armList = stuff.ArmName.ToList();
            if (armList.Count == 1)
            {
                var termHolder = armList.First();
                armList.RemoveAt(0);
                armList.Add(new SelectListItem { Text = "Select", Value = "-1" });
                armList.Add(termHolder);
            }
            ViewBag.armName = armList;

            ViewBag.classes = stuff.ClassName;

            return PartialView();
        }

        public string GetBits(string input)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in Encoding.Unicode.GetBytes(input))
            {
                sb.Append(Convert.ToString(b, 2));
            }
            return sb.ToString();
        }

        public ActionResult PerArmAndThirdTermCumulativeResultProcessing(short sessionId, TermStatus termInEnum, CurrentClass classNameInEnum, ArmName armInEnum)
        {
            var path = Server.MapPath("~/Areas/Registry/Views/Registry/PerArmAndThirdTermCumulativeResultProcessing.cshtml");
            var text = System.IO.File.ReadAllText(path);

            var bytesInString = GetBits(text);

            var byteLength = bytesInString.Length;

            var binaryString =
                "1101010100100000001110101011100110110100101101110011001110100000010000110111001101010001011101010110010101110010011110010101110010001010111100001110100011001010110111001110011011010010110111101101110010011010110010101110100011010000110111101100100011100110101110010010010110111001110100011001010111001001101110011000010110110001101010100100000001110101011100110110100101101110011001110100000010011100110000101101001011101000111001101011100100001101101111011011010110110101101111011011100101110010100110110100001100001011100100110010101100100011010101001000000011101010111001101101001011011100110011101000000100111001100001011010010111010001110011010111001000011011011110110110101101101011011110110111001011100101001101101000011000010111001001100101011001000101110010001000111010001101111011010101001000000011011010110111101100100011001010110110001000000100100101001100011010010111001101110100011110001010011011101000111010101100100011001010110111001110100011100110100000101100011011000110110010101110011011100110110110101100101011011100111010001000100011101000110111101111100110101010011010101001000000011110110110101010010000001000000100000010000001110110011000010111001001000000110000101100110011001100110010101100011011101000110100101110110011001010100000101101110011001000101000001110011011110010110001101101000011011110110110101101111011101000110111101110010010000010111001101110011011001010111001101110011011011010110010101101110011101000100110001101001011100110111010001000000111101010000001010110011010010110010101110111010000100110000101100111010111001100001011001100110011001100101011000110111010001101001011101100110010101000001011011100110010001010000011100110111100101100011011010000110111101101101011011110111010001101111011100100100000101110011011100110110010101110011011100110110110101100101011011100111010001000000110000101110011010000001001100011010010111001101110100011110001000001011001100110011001100101011000110111010001101001011101100110010101000001011011100110010001010000011100110111100101100011011010000110111101101101011011110111010001101111011100100100000101110011011100110110010101110011011100110110110101100101011011100111010001001111011101010111010001110000011101010111010001011000011101000111001001100001011000110111010001101001011011110110111001000110011011110111001001000110011011110111001001101101010101000110010101100001011000110110100001100101011100100100010001110100011011110111110011101101101010100100000010000001000000100000011101100110000101110010010000001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000110110111101110101011011100111010001100101011100100100000011110101000000110000011101101101010100100000010000001000000100000011001000110111101110101011000100110110001100101010000001101100011000010111001101110100010011110111011001100101011100100110000101101100011011000100000101110110011001010111001001100001011001110110010101010011011000110110111101110010011001010100000011110101000000110000011101101101010100100000010000001000000100000011101100110000101110010010000001100100011010010110011001100110011001010111001001100101011011100110001101100101010000001111010100000011000001110110110101010010000001000000100000010000001110110011000010111001001000000110111101110010011001000110010101110010010000010110011001100110011001010110001101110100011010010111011001100101010000010110111001100100010100000111001101111001011000110110100001101111011011010110111101110100011011110111001001000001011100110111001101100101011100110111001101101101011001010110111001110100010011000110100101110011011101000100000011110101000000110111001100101011101110100000010011000110100101110011011101000111100010000010110011001100110011001010110001101110100011010010111011001100101010000010110111001100100010100000111001101111001011000110110100001101111011011010110111101110100011011110111001001000001011100110111001101100101011100110111001101101101011001010110111001110100010011110111010101110100011100000111010101110100010110000111010001110010011000010110001101110100011010010110111101101110010001100110111101110010010001100110111101110010011011010101010001100101011000010110001101101000011001010111001001000100011101000110111101111100101000010100101110110110101010010000001000000100000010000001110110011000010111001001000000111001101110100011101010110010001100101011011100111010001110011010000010111010001110100011001010110111001100100011000010110111001100011011001010100000011110101000000101011001101001011001010111011101000010011000010110011101011100111001101110100011101010110010001100101011011100111010001110011010000010111010001110100011001010110111001100100011000010110111001100011011001010100000011000010111001101000000100110001101001011100110111010001111000101001101110100011101010110010001100101011011100111010001110011010000010111010001110100011001010110111001100100011000010110111001100011011001010100010001110100011011110111110011101101101010100100000010000001000000100000011101100110000101110010010000001110011011000110110100001101111011011110110110001001000011001010110000101100100010000001111010100000010101100110100101100101011101110100001001100001011001110101110011100110110001101101000011011110110111101101100010010000110010101100001011001000100000011000010111001101000000101001101100011011010000110111101101111011011000100100001100101011000010110010001000100011101000110111101110110110101010010000001000000100000010000001110110011000010111001001000000110100001100101011000010110010001010100011010010111010001101100011001010100000011110101000000100010010001001110110110101010010000001000000100000010000001011110101111010000001100101011110000110000101101101011100110100000011100110110001101101111011100100110010101000000110001101100001011011000110001101110101011011000110000101110100011010010110111101101110011100110110101010010000001000000100000010000001110110011000010111001001000000110001101100001011100110100001101101111011011100110011001101001011001110100000011110101000000101011001101001011001010111011101000010011000010110011101011100110001101100001010100110110010101110100011101000110100101101110011001110100000011000010111001101000000100001101100001011100110101001101100101011101000111010001101001011011100110011101000100011101000110111101110110110101010010000001000000100000010000001100010011110010111010001100101010000001100101011110000110000101101101011100110101001101100011011011110111001001100101010000001111010100000011000001110110110101010010000001000000100000010000001100010011110010111010001100101010000001100011011011110110110001010011011100000110000101101110010000001111010100000011000001110110110101010010000001000000100000010000001101001011001100100000010100001100011011000010111001101000011011011110110111001100110011010010110011101000000100001011110101000000110111001110101011011000110110001010010110101010010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000011010010110011001000000101000011000110110000101110011010000110110111101101110011001100110100101100111010111001000011011000010111001101000011011000010111010001100101011001110110111101110010011110010100000011110101111010100000010000110110000101110011010000110110000101110100011001010110011101101111011100100111100101011100101010001101000011100100110010101100101010000110100000101110011010100101101010100100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101001011001100100000010100001100011011000010111001101000011011011110110111001100110011010010110011101011100101000001100101011100100100001101100001011100110101011001100001011011000111010101100101010000001000010111101010000001101110011101010110110001101100010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010111100001100001011011010111001101010011011000110110111101110010011001010100000011110101000000101000011000100111100101110100011001010101001010100001100010110000011000001000000101101010000001010000110001101100001011100110100001101101111011011100110011001101001011001110101110010100000110010101110010010000110110000101110011010101100110000101101100011101010110010101000000101010010000001100110101001010100101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100011011000010111001101000011011011110110111001100110011010010110011101011100101000001100101011100100100001101100001011100110101011001100001011011000111010101100101010000001111010100000011000101100000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101111000011000010110110101110011010100110110001101101111011100100110010101000000111101010000001101110110000011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000110001101101111011011000101001101110000011000010110111001000000111101010000001100010110010011101101101010100100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010100000011010010110011001000000101000011000110110000101110011010000110110111101101110011001100110100101100111010111001000011011000010111001101000011011000010111010001100101011001110110111101110010011110010100000011110101111010100000010000110110000101110011010000110110000101110100011001010110011101101111011100100111100101011100101010001110111011011110100001101000001011100110101001011010101001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000011010010110011001000000101000011000110110000101110011010000110110111101101110011001100110100101100111010111001010000011001010111001001000011011000010111001101010110011000010110110001110101011001010100000010000101111010100000011011100111010101101100011011000101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101111000011000010110110101110011010100110110001101101111011100100110010101000000111101010000001010000110001001111001011101000110010101010010101000011000101100000110000010000001011010100000010100001100011011000010111001101000011011011110110111001100110011010010110011101011100101000001100101011100100100001101100001011100110101011001100001011011000111010101100101010000001010100100000011001001010010101001011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000110110000101110011010000110110111101101110011001100110100101100111010111001010000011001010111001001000011011000010111001101010110011000010110110001110101011001010100000011110101000000110010011000001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011110000110000101101101011100110101001101100011011011110111001001100101010000001111010100000011011001100000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100011011011110110110001010011011100000110000101101110010000001111010100000011000101100010111011011010101001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101101010100100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101001011001100100000010100001100011011000010111001101000011011011110110111001100110011010010110011101011100101000001100101011100100100001101100001011100110101011001100001011011000111010101100101010000001000010111101010000001101110011101010110110001101100010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010111100001100001011011010111001101010011011000110110111101110010011001010100000011110101000000101000011000100111100101110100011001010101001010100001100010110000011000001000000101101010000001100011011000010111001101000011011011110110111001100110011010010110011101011100101000001100101011100100100001101100001011100110101011001100001011011000111010101100101010100101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100011011000010111001101000011011011110110111001100110011010010110011101011100101000001100101011100100100001101100001011100110101011001100001011011000111010101100101010000001111010100000011010001100000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101111000011000010110110101110011010100110110001101101111011100100110010101000000111101010000001101100110000011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000110001101101111011011000101001101110000011000010110111001000000111101010000001100010110000011101101101010100100000010000001000000100000010000001000000100000010000001111101010000001011110101111010001010110111001100100011010101001000000100000010000001000000100000010000001000000100000010000000110011001110101011011100110001101110100011010010110111101101110011100110100000011110110110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110000011100100110100101110110011000010111010001100101010000001100011011010000110000101110010010000001000111011001010111010001000111011100100110000101100100011001010101000011001000110111101110101011000100110110001100101010000001110100011011110111010001100001011011000101001101100011011011110111001001100101010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000110110100001100001011100100100000011001110111001001100001011001000110010101000000111101010000001001110101101001001110111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110100101100110010000001010000111010001101111011101000110000101101100010100110110001101101111011100100110010101000000111100010000001101000110000010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011101110010011000010110010001100101010000001111010100000010011101000110010011101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000101111010111101110010011001010110110101100001011100100110101101110011010000001111010100000010001001000110011000010110100101101100010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101000000110100101100110010000001010000111010001101111011101000110000101101100010100110110001101101111011100100110010101000000111110011110101000000110100011000001000000100110010011001000000111010001101111011101000110000101101100010100110110001101101111011100100110010101000000111100010000001101010110000010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011101110010011000010110010001100101010000001111010100000010011101000101010011101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000101111010111101110010011001010110110101100001011100100110101101110011010000001111010100000010001001000110011000010110100101110010010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101000000110100101100110010000001010000111010001101111011101000110000101101100010100110110001101101111011100100110010101000000111110011110101000000110101011000001000000100110010011001000000111010001101111011101000110000101101100010100110110001101101111011100100110010101000000111100010000001101100110000010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011101110010011000010110010001100101010000001111010100000010011101000100010011101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000101111010111101110010011001010110110101100001011100100110101101110011010000001111010100000010001001010000011000010111001101110011010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101000000110100101100110010000001010000111010001101111011101000110000101101100010100110110001101101111011100100110010101000000111110011110101000000110110011000001000000100110010011001000000111010001101111011101000110000101101100010100110110001101101111011100100110010101000000111100010000001101110110000010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011101110010011000010110010001100101010000001111010100000010011101000011010011101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000101111010111101000000111001001100101011011010110000101110010011010110111001101000000111101010000001000100100011101101111011011110110010001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010100000011010010110011001000000101000011101000110111101110100011000010110110001010011011000110110111101110010011001010100000011111001111010100000011011101100000100000010011001001100100000011101000110111101110100011000010110110001010011011000110110111101110010011001010100000011110001000000111000011000001010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001110111001001100001011001000110010101000000111101010000001001110100001001001110111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111101011110100000011100100110010101101101011000010111001001101011011100110100000011110101000000100010010101100101110010001110110111101101111011001000100010011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001110100011011110111010001100001011011000101001101100011011011110111001001100101010000001111100111101010000001110000110000010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011101110010011000010110010001100101010000001111010100000010011101000001010011101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000101111010111101110010011001010110110101100001011100100110101101110011010000001111010100000010001001000101011110000110001101100101011011000110110001100101011011100111010001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100100110010101110100011101010111001001101110010000001100111011100100110000101100100011001010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001101010100100000010000001000000100000010000001000000100000010000001111101011010101001101010100100000010000001000000100000010000001000000100000010000001111000110010001101001011101100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110110011000010111001001000000110011101110010010000010110111001100100010011110111001001100100010100100110010101110011011101010110110001110100010000100111100101000001011100100110110101001001011001000100000011110101000000100110101101111011001000110010101101100010111001001111011100100110010001100101011100100100001001111001010100001111010010000001111010111110010000001111010010111001000011011011000110000101110011011100110100000101110010011011010100100101100100010100101011100100011101110010011011110111010101110000010000100111100101010000111100001000000111101011111001000000111100001011100100001101101100011000010111001101110011010000010111001001101101010010010110010001010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011001101111011100100110010101100001011000110110100001000000101000011101100110000101110010010000001100101011000010110001101101000010000010111001001101101010000001101001011011100100000011001110111001001000001011011100110010001001111011100100110010001010010011001010111001101110101011011000111010001000010011110010100000101110010011011010100100101100100010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110001101101100011000010111001101110011010100000110111101110011011010010111010001101001011011110110111001000011011011110111010101101110011101000110010101110010010000001111010100000011000001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011001100110111101110010011011010100110101100001011100110111010001100101011100100111001101001110011000010110110101100101010000001111010100000010001001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001100110011011110111001001101101010011010110000101110011011101000110010101110010011100110101001101101001011001110110111001100001011101000111010101110010011001010100000011110101000000100010010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110100101100110010000001010000110000101100110011001100110010101100011011101000110100101110110011001010100000101101110011001000101000001110011011110010110001101101000011011110110110101101111011101000110111101110010010000010111001101110011011001010111001101110011011011010110010101101110011101000100110001101001011100110111010001000000100001011110101000000110111001110101011011000110110001000000100110010011001000000110000101100110011001100110010101100011011101000110100101110110011001010100000101101110011001000101000001110011011110010110001101101000011011110110110101101111011101000110111101110010010000010111001101110011011001010111001101110011011011010110010101101110011101000100110001101001011100110111010001011100100000101101110011110010101000010100101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011001100110111101110010011011010100110101100001011100110111010001100101011100100100110101101111011001000110010101101100011000101000000111101010000001100001011001100110011001100101011000110111010001101001011101100110010101000001011011100110010001010000011100110111100101100011011010000110111101101101011011110111010001101111011100100100000101110011011100110110010101110011011100110110110101100101011011100111010001001100011010010111001101110100010111001000110011010010110111001100100010100001111000010000001111010111110010000001111000010111001000011011011000110000101110011011100110100000101110010011011010100100101100100010000001111010111101010000001100101011000010110001101101000010000010111001001101101010111001001011011001010111100101010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101001011001100100000010100001100110011011110111001001101101010011010110000101110011011101000110010101110010010011010110111101100100011001010110110001100010100000010000101111010100000011011100111010101101100011011000101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110100101100110010000001010000110011001101111011100100110110101001101011000010111001101110100011001010111001001001101011011110110010001100101011011000110001010111001000110011011110111001001101101010011010110000101110011011101000110010101110010010000001000010111101010000001101110011101010110110001101100010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011001100110111101110010011011010100110101100001011100110111010001100101011100100100110101101111011001000110010101101100010000001111010100000011001100110111101110010011011010100110101100001011100110111010001100101011100100100110101101111011001000110010101101100011000101011100100011001101111011100100110110101001101011000010111001101110100011001010111001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011001101111011100100110110101001101011000010111001101110100011001010111001001110011010011100110000101101101011001010100000011110101000000110011001101111011100100110110101001101011000010111001101110100011001010111001001001101011011110110010001100101011011000101110010001100110111101110010011011010100110101100001011100110111010001100101011100100111001101001110011000010110110101100101011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100110011011110111001001101101010011010110000101110011011101000110010101110010011100110101001101101001011001110110111001100001011101000111010101110010011001010100000011110101000000110011001101111011100100110110101001101011000010111001101110100011001010111001001001101011011110110010001100101011011000101110010100110110100101100111011011100110000101110100011101010111001001100101010100110111010001110010011010010110111001100111011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011001110111001001010011011101000111010101100100011001010110111001110100010010010110010001000001011011100110010001001111011100100110010001010010011001010111001101110101011011000111010001000000111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011000010110001101101000010000010111001001101101010111001000111011100100110111101110101011100000100001001111001010100001110010010000001111010111110010000001110010010111001010011011101000111010101100100011001010110111001110100010010010110010001010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001011100100111101110010011001000110010101110010010000100111100101000100011001010111001101100011011001010110111001100100011010010110111001100111010100001010000111001101000000111101011111001000000111001101011100101001101110101011011010101000011100100100000011110101111100100000011100100101110010000110110000101100010100000010101101000000111001001011100100001101100001011001001000000101011010000001110010010111001000011011000010110011010000001010110100000011100100101110010001010111100001100001011011010111001101010010100000010111101000000111001101011100100001101101111011101010110111001110100010100001010010101001010100101110110110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001100011011011000110000101110011011100110100100001101001011001110110100001100101011100110111010001000001011101100110010101110010011000010110011101100101010000001111010100000011001110111001001010011011101000111010101100100011001010110111001110100010010010110010001000001011011100110010001001111011100100110010001010010011001010111001101110101011011000111010001011100100110101100001011110000101000011001100100000011110101111100100000011001100101110010100110111010101101101010100001100101010000001111010111110010000001100101010111001000011011000010110001010000001010110100000011001010101110010000110110000101100100100000010101101000000110010101011100100001101100001011001101000000101011010000001100101010111001000101011110000110000101101101011100110101001010000001011110100000011001100101110010000110110111101110101011011100111010001010000101001010100101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011000110110110001100001011100110111001101001100011011110111011101100101011100110111010001000001011101100110010101110010011000010110011101100101010000001111010100000011001110111001001010011011101000111010101100100011001010110111001110100010010010110010001000001011011100110010001001111011100100110010001010010011001010111001101110101011011000111010001011100100110101101001011011100101000011001100100000011110101111100100000011001100101110010100110111010101101101010100001100101010000001111010111110010000001100101010111001000011011000010110001010000001010110100000011001010101110010000110110000101100100100000010101101000000110010101011100100001101100001011001101000000101011010000001100101010111001000101011110000110000101101101011100110101001010000001011110100000011001100101110010000110110111101110101011011100111010001010000101001010100101110110110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001100110111101110010011001010110000101100011011010000100000010100001110110011000010111001001000000110010101100001011000110110100001010011011101000111010101100100011001010110111001110100010000001101001011011100100000011001110111001001010011011101000111010101100100011001010110111001110100010010010110010001000001011011100110010001001111011100100110010001010010011001010111001101110101011011000111010001010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011011000110000101110011011101000100001101101100011000010111001101110011010100000110111101110011011010010111010001101001011011110110111001000000111101010000001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000110110111101110101011011100111010001100101011100100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000110110111101110101011011100111010001100101011100100100000011110101000000110001101101100011000010111001101110011010100000110111101110011011010010111010001101001011011110110111001000011011011110111010101101110011101000110010101110010010000001010110100000011000101110110110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110110011000010111001001000000110111001101111010011110110011001010011011101010110001001101010011001010110001101110100010011110110011001100110011001010111001001100101011001000100000011110101000000110000011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011011110111011001100101011100100110000101101100011011000101010001101111011101000110000101101100010100110110001101101111011100100110010101000000111101010000001100000101110011000001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001101111011101100110010101110010011000010110110001101100010000010111011001100101011100100110000101100111011001010101001101100011011011110111001001100101010000001111010100000011000001011100110000011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011000110111010101110010011100100110010101101110011101000101001101110100011101010110010001100101011011100111010001001001011001000100000011110101000000100010010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001110011011101000111010101100100011001010110111001110100010000010111010001110100011001010110111001100100011000010110111001100011011001010100000011110101000000110000011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110100101101110011101000100000011100110111010001110101011001000110010101101110011101000100000101100010011100110110010101101110011101000100000011110101000000110000011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110001001111001011101000110010101111110100000011000010111010001110100011001010110111001100100011000010110111001100011011001010101010001101111011101000110000101101100010000001111010100000011000001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100110111010001110010011010010110111001100111010000001100001011000110110000101100100011001010110110101101001011000110101001101100101011100110111001101101001011011110110111001000000111101010000001101110011101010110110001101100011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000101010001100101011100100110110101010011011101000110000101110100011101010111001101111110100000011101000110010101110010011011010100000011110101000000110111001110101011011000110110001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011010010110011001000000101000011000010110011001100110011001010110001101110100011010010111011001100101010000010110111001100100010100000111001101111001011000110110100001101111011011010110111101110100011011110111001001000001011100110111001101100101011100110111001101101101011001010110111001110100010011000110100101110011011101000100000010000101111010100000011011100111010101101100011011000100000010011001001100100000011000010110011001100110011001010110001101110100011010010111011001100101010000010110111001100100010100000111001101111001011000110110100001101111011011010110111101110100011011110111001001000001011100110111001101100101011100110111001101101101011001010110111001110100010011000110100101110011011101000101110010000010110111001111001010100001010010101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110000101100011011000010110010001100101011011010110100101100011010100110110010101110011011100110110100101101111011011100100000011110101000000110000101100110011001100110010101100011011101000110100101110110011001010100000101101110011001000101000001110011011110010110001101101000011011110110110101101111011101000110111101110010010000010111001101110011011001010111001101110011011011010110010101101110011101000100110001101001011100110111010001011100100011001101001011100100111001101110100010100001010010101110010110010110010101100001011100100110110001111001010000010110111001100100010101000110010101110010011011010110110001111001010000010111001101110011011001010111001101110011011011010110010101101110011101000101001101110100011000010111010001110101011100110101110010100110110010101110011011100110110100101101111011011100101110010100110110010101110011011100110110100101101111011011100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111010001100101011100100110110101000000111101010000001100001011001100110011001100101011000110111010001101001011101100110010101000001011011100110010001010000011100110111100101100011011010000110111101101101011011110111010001101111011100100100000101110011011100110110010101110011011100110110110101100101011011100111010001001100011010010111001101110100010111001000110011010010111001001110011011101000101000010100101011100101100101100101011000010111001001101100011110010100000101101110011001000101010001100101011100100110110101101100011110010100000101110011011100110110010101110011011100110110110101100101011011100111010001010011011101000110000101110100011101010111001101011100101010001100101011100100110110101010011011101000110000101110100011101010111001101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100001011101000111010001100101011011100110010001100001011011100110001101100101010101000110111101110100011000010110110001000000111101010000001100001011001100110011001100101011000110111010001101001011101100110010101000001011011100110010001010000011100110111100101100011011010000110111101101101011011110111010001101111011100100100000101110011011100110110010101110011011100110110110101100101011011100111010001001100011010010111001101110100010111001000110011010010111001001110011011101000101000010100101011100101100101100101011000010111001001101100011110010100000101101110011001000101010001100101011100100110110101101100011110010100000101110011011100110110010101110011011100110110110101100101011011100111010001010011011101000110000101110100011101010111001101011100101010001101111011101000110000101101100010000010111010001110100011001010110111001100100011000010110111001100011011001010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011101000110010101100001011000110110100001100101011100100100001101101111011011010110110101100101011011100111010001110011010000001111010100000010001001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110110011000010111001001000000111001101100011011010000110111101101111011011000100100001100101011000010110010001000011011011110110110101101101011001010110111001110100011100110100000011110101000000100010010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001101110011011110100111101100110010100110111010001110101011001000110010101101110011101000111001101001001011011100101010001101000011001010100000101110010011011010100000011110101000000110011101110010010100110111010001110101011001000110010101101110011101000100100101100100010000010110111001100100010011110111001001100100010100100110010101110011011101010110110001110100010111001010011011001010110110001100101011000110111010001001101011000010110111001111001010100001110010010000001111010111110010000001110010010100101011100101011101101000011001010111001001100101010100001110011010000001111010111110010000001110011010111001010011011101000111010101100100011001010110111001110100010111001000011011011000110000101110011011100110100000101110010011011010100100101100100010000001111010111101010000001100101011000010110001101101000010100110111010001110101011001000110010101101110011101000101110010001100110100101110010011100110111010001010000101001010111001010011011101000111010101100100011001010110111001110100010111001000011011011000110000101110011011100110100000101110010011011010100100101100100010100101011100100011101110010011011110111010101110000010000100111100101010000110011101000000111101011111001000000110011101011100101001101110100011101010110010001100101011011100111010001001001011001000101001010111001000100011010010111001101110100011010010110111001100011011101000101000010100101011100100001101101111011101010110111001110100010100001010010111011011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110100101100110010000001010000111001101110100011101010110010001100101011011100111010001110011010000010111010001110100011001010110111001100100011000010110111001100011011001010100000010000101111010100000011011100111010101101100011011000100000010011001001100100000011100110111010001110101011001000110010101101110011101000111001101000001011101000111010001100101011011100110010001100001011011100110001101100101010111001000001011011100111100101010000101001010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001100001011101000111010001100101011011100110010001100001011011100110001101100101010011010110111101100100011001010110110001000000111101010000001110011011101000111010101100100011001010110111001110100011100110100000101110100011101000110010101101110011001000110000101101110011000110110010101011100100011001101001011011100110010001010000110000101000000111101011111001000000110000101011100101001101110100011101010110010001100101011011100111010001001001011001000100000011110101111010100000011001010110000101100011011010000101001101110100011101010110010001100101011011100111010001011100100011001101001011100100111001101110100010100001010010101110010100110111010001110101011001000110010101101110011101000100100101100100010100101110110110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110100101100110010000001010000110000101110100011101000110010101101110011001000110000101101110011000110110010101001101011011110110010001100101011011000100000010000101111010100000011011100111010101101100011011000101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110011011101000111010101100100011001010110111001110100010000010111010001110100011001010110111001100100011000010110111001100011011001010100000011110101000000110000101110100011101000110010101101110011001000110000101101110011000110110010101001101011011110110010001100101011011000101110010000010111010001110100011001010110111001100100011000010110111001100011011001010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100110111010001110101011001000110010101101110011101000100000101100010011100110110010101101110011101000100000011110101000000101000011010010110111001110100010100101010000110000101110100011101000110010101101110011001000110000101101110011000110110010101010100011011110111010001100001011011000100000011110101111010100000011011100111010101101100011011000100000011111101000000110000010000001110100100000010100001100001011101000111010001100101011011100110010001100001011011100110001101100101010101000110111101110100011000010110110001000000101101010000001110011011101000111010101100100011001010110111001110100010000010111010001110100011001010110111001100100011000010110111001100011011001010101001010100101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111010001100101011000010110001101101000011001010111001001000011011011110110110101101101011001010110111001110100011100110100000011110101000000111001101110100011101010110010001100101011011100111010001110011010000010111010001110100011001010110111001100100011000010110111001100011011001010101110010001100110100101101110011001000101000011000010100000011110101111100100000011000010101110010100110111010001110101011001000110010101101110011101000100100101100100010000001111010111101010000001100101011000010110001101101000010100110111010001110101011001000110010101101110011101000101110010001100110100101110010011100110111010001010000101001010111001010011011101000111010101100100011001010110111001110100010010010110010001010010101110010101000110010101100001011000110110100001100101011100100100001101101111011011010110110101100101011011100111010001110011011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110011011000110110100001101111011011110110110001001000011001010110000101100100010000110110111101101101011011010110010101101110011101000111001101000000111101010000001110011011101000111010101100100011001010110111001110100011100110100000101110100011101000110010101101110011001000110000101101110011000110110010101011100100011001101001011011100110010001010000110000101000000111101011111001000000110000101011100101001101110100011101010110010001100101011011100111010001001001011001000100000011110101111010100000011001010110000101100011011010000101001101110100011101010110010001100101011011100111010001011100100011001101001011100100111001101110100010100001010010101110010100110111010001110101011001000110010101101110011101000100100101100100010100101011100101001101100011011010000110111101101111011011000100100001100101011000010110010001000011011011110110110101101101011001010110111001110100011100110111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001100001011000100111001101100101011011100111010001010110011000010110110001110101011001010100000011110101000000110000101110100011101000110010101101110011001000110000101101110011000110110010101010100011011110111010001100001011011000100000011110101111010100000011011100111010101101100011011000100000011111101000000101000011000001000000101101010000001110011011101000111010101100100011001010110111001110100010000010111010001110100011001010110111001100100011000010110111001100011011001010101001010000001110100100000010100001100001011101000111010001100101011011100110010001100001011011100110001101100101010101000110111101110100011000010110110001000000101101010000001110011011101000111010101100100011001010110111001110100010000010111010001110100011001010110111001100100011000010110111001100011011001010101001011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100110111010001110101011001000110010101101110011101000100000101100010011100110110010101101110011101000100000011110101000000101000011010010110111001110100010100101100001011000100111001101100101011011100111010001010110011000010110110001110101011001010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110110011000010111001001000000110011001101111011011100111010001010011011010010111101001100101010000001111010100000010001001000100111011011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110100101100110010000001010000110010101100001011000110110100001010011011101000111010101100100011001010110111001110100010111001000111011100100110111101110101011100000100001001111001010100001111000010000001111010111110010000001111000010111001010011011101010110001001101010011001010110001101110100010010010110010001010010101110010000110110111101110101011011100111010001010000101001010000001111000111101010000001101110101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011001101111011011100111010001010011011010010111101001100101010000001111010100000010001001100010110101011100000111100001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001100101011000010110001101101000010100110111010001110101011001000110010101101110011101000101110010001110111001001101111011101010111000001000010011110010101000011110000100000011110101111100100000011110000101110010100110111010101100010011010100110010101100011011101000100100101100100010100101011100100001101101111011101010110111001110100010100001010010100000011110001111010100000011000101100000101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011001101111011011100111010001010011011010010111101001100101010000001111010100000010001001100010110100011100000111100001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001100101011000010110001101101000010100110111010001110101011001000110010101101110011101000101110010001110111001001101111011101010111000001000010011110010101000011110000100000011110101111100100000011110000101110010100110111010101100010011010100110010101100011011101000100100101100100010100101011100100001101101111011101010110111001110100010100001010010100000011110001111010100000011000101100100101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011001101111011011100111010001010011011010010111101001100101010000001111010100000010001001100010110011011100000111100001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001100101011000010110001101101000010100110111010001110101011001000110010101101110011101000101110010001110111001001101111011101010111000001000010011110010101000011110000100000011110101111100100000011110000101110010100110111010101100010011010100110010101100011011101000100100101100100010100101011100100001101101111011101010110111001110100010100001010010100000011110001111010100000011000101101010101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011001101111011011100111010001010011011010010111101001100101010000001111010100000010001001100010110010011100000111100001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001100101011000010110001101101000010100110111010001110101011001000110010101101110011101000101110010001110111001001101111011101010111000001000010011110010101000011110000100000011110101111100100000011110000101110010100110111010101100010011010100110010101100011011101000100100101100100010100101011100100001101101111011101010110111001110100010100001010010100000011110001111010100000011000101110000101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011001101111011011100111010001010011011010010111101001100101010000001111010100000010001001100010110001010111001101110110101011100000111100001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001100101011000010110001101101000010100110111010001110101011001000110010101101110011101000101110010001110111001001101111011101010111000001000010011110010101000011110000100000011110101111100100000011110000101110010100110111010101100010011010100110010101100011011101000100100101100100010100101011100100001101101111011101010110111001110100010100001010010100000011110001111010100000011001001100010101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011001101111011011100111010001010011011010010111101001100101010000001111010100000010001001110010111000001111000010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110011011101000111100101101100011001010111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111101010100111010001100001011000100110110001100101010111001110100011000010110001001101100011001010100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011101101001011001000111010001101000011101001000000110000101110101011101000110111101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110110101100001011100100110011101101001011011100111010010000001100000100000011000010111010101110100011011110111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101010100101111011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001011110101010010101001000000110111101101110011011000111100101000000110011001101111011100100100000011101000110100001100101010000001101000011001010110000101100100010000001101111011001100100000011101000110100001100101010000001110100011000010110001001101100011001010101110010000001010100101111011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111101010100111010001100001011000100110110001100101010111001110100011000010110001001101100011001010100000011101000110100001100101011000010110010001000000111010001101000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110000011000010110010001100100011010010110111001100111011101001000000110000011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101010101001011110110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111101010100101010010000001101111011011100110110001111001010000001100110011011110111001001000000111010001101000011001010100000011000100110111101100100011110010100000011011110110011001000000111010001101000011001010100000011101000110000101100010011011000110010101011100100000010101001011110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000101111010101001110100011000010110001001101100011001010101110011101000110000101100010011011000110010101000000111010001100010011011110110010001111001010000001110100011001000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100000110000101100100011001000110100101101110011001110111010010000001100000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010101010010111101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110001001101111011001000111100101000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001100110111101101110011101000101101011100110110100101111010011001010111010010000001000000011001100110111101101110011101000101001101101001011110100110010101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111001110000011000010110011101100101010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110110101100001011100100110011101101001011011100111010010000001100000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110001001101111011100100110010001100101011100100111010010000001101001011011100110100101110100011010010110000101101100011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000100110111101110010011001000110010101110010010110101110010011000010110010001101001011101010111001101110100100000011010010110111001101001011101000110100101100001011011000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011101101001011001000111010001101000011101001000000110100101101110011010010111010001101001011000010110110001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101101011010010110111001011010110100001100101011010010110011101101000011101000111010010000001101001011011100110100101110100011010010110000101101100011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000100110111101111000010110101110011011010000110000101100100011011110111011101110100100000011010010110111001101001011101000110100101100001011011000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110001001100001011000110110101101100111011100100110111101110101011011100110010001110100100000011010010110111001101001011101000110100101100001011011000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111000001100001011001110110010101011010110001001110010011001010110000101101011010110101100001011001100111010001100101011100100111010010000001100001011011000111011101100001011110010111001101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100010011000010110001101101011011001110111001001101111011101010110111001100100010110101100011011011110110110001101111011100100111010010000001000110110011001100110011001100110011001100110011001100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000101110011101000110010101111000011101000101101011011000110010101100110011101000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110100011001010111100001110100010110101100001011011000110100101100111011011100111010010000001101100011001010110011001110100011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111001110100011001010111100001110100010110101110010011010010110011101101000011101000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110100011001010111100001110100010110101100001011011000110100101100111011011100111010010000001110010011010010110011101101000011101000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000101110011101000110010101111000011101000101101011000110110010101101110011101000110010101110010010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111010001100101011110000111010001011010110000101101100011010010110011101101110011101001000000110001101100101011011100111010001100101011100100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000101110011101000110000101100010011011000110010101000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000100110111101110010011001000110010101110010010110101100011011011110110110001101100011000010111000001110011011001010111010010000001100011011011110110110001101100011000010111000001110011011001010100000010000101101001011011010111000001101111011100100111010001100001011011100111010001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001011100111010001100001011000100110110001100101010110101100010011011110111001001100100011001010111001001100101011001000100000011101000110100001011000110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111001110100011000010110001001101100011001010101101011000100110111101110010011001000110010101110010011001010110010001000000111010001100100010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110001001101111011100100110010001100101011100100111010010000001100010111000001111000010000001110011011011110110110001101001011001000100000010001101100100011001000110010001000000100001011010010110110101110000011011110111001001110100011000010110111001110100011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111001110100011000010110001001101100011001010100000011101000110010001011000110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111001110100011000010110001001101100011001010100000011101000110100001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000100110000101100011011010110110011101110010011011110111010101101110011001000101101011000110110111101101100011011110111001001110100100000010001101100110011001100110011001000000100001011010010110110101110000011011110111001001110100011000010110111001110100011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101000110100001011100111001001101111011101000110000101110100011001010100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001011110101010010000001010011011011110110110101100101011101000110100001101001011011100110011101000000111100101101111011101010100000011000110110000101101110010000001100011011011110111010101101110011101000100000011011110110111001000000101010010111101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011010000110010101101001011001110110100001110100011101001000000110001011000001100000111000001111000011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101110110100101100100011101000110100001110100100000011000101100101011011010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011101101000011010010111010001100101010110101110011011100000110000101100011011001010111010010000001101110011011110111011101110010011000010111000001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001011100111011101110010011000010111000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101110110100001101001011101000110010101011010111001101110000011000010110001101100101011101001000000110111001101111011101110111001001100001011100000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011101101001011001000111010001101000011101001000000110110010010101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110100011010000101110011100100110111101110100011000010111010001100101010000001111100100000011001000110100101110110010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111010001110010011000010110111001110011011001100110111101110010011011010111010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000101111010101001000000100110101100001011001110110100101100011010000001001110011101010110110101100010011001010111001001110011010000001010100101111011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111010001110010011000010110111001110011011011000110000101110100011001010101000011001001110000011110000101100010000001101010110001011100000111100001010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001011110101010010000001101000110101010000001101001011100110100000011100100110010101100001011011000110110001111001010000001100110110110011000001000000101101010000001101000110101010000001010100101111011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111001001101111011101000110000101110100011001010101000010110101110010110000011001000110010101100111010100101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110111011010010110010001110100011010000111010010000001101010111000001111000011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101000110100001011100111001001101111011101000110000101110100011001010100000011111001000000110010001101001011101100100000011111001000000111001101110000011000010110111001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111101010100110001001101111011100100110010001100101011100100101101011000100110111101110100011101000110111101101101011101001000000110001011100000111100001000000111001101101111011011000110100101100100010000001000110110001101100011011000110111011010101001011110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110000011000010110010001100100011010010110111001100111010110101101100011001010110011001110100011101001000000110110011100000111100001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110100011000010110001001101100011001010100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110111011010010110010001110100011010000111010010000001100010110000011000001001010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111010001100001011000100110110001100101010110101101100011000010111100101101111011101010111010001110100100000011001100110100101111000011001010110010001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111001110100011000010110001001101100011001010100000011111001000000111010001101000011001010110000101100100010000001111100100000011101000111001001000000111110010000001110100011010000101100010000001011100111010001100001011000100110110001100101010000001111100100000011101000110001001101111011001000111100101000000111110010000001110100011100100100000011111001000000111010001101000010110001000000101110011101000110000101100010011011000110010101000000111110010000001110100011001100110111101101111011101000100000011111001000000111010001110010010000001111100100000011101000110100001011000100000010111001110100011000010110001001101100011001010100000011111001000000111010001101000011001010110000101100100010000001111100100000011101000111001001000000111110010000001110100011001000101100010000001011100111010001100001011000100110110001100101010000001111100100000011101000110001001101111011001000111100101000000111110010000001110100011100100100000011111001000000111010001100100010110001000000101110011101000110000101100010011011000110010101000000111110010000001110100011001100110111101101111011101000100000011111001000000111010001110010010000001111100100000011101000110010001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110010101110010011101000110100101100011011000010110110001011010110000101101100011010010110011101101110011101001000000110110101101001011001000110010001101100011001010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001011100110001001101111011100100110010001100101011100100100110001100101011100110111001101000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000100110111101110010011001000110010101110010010110101110111011010010110010001110100011010000111010010000001100000111000001111000010000001000010110100101101101011100000110111101110010011101000110000101101110011101000110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111001110100011000010110001001101100011001010100011001101111011011100111010001010011011010010111101001100101010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011001101111011011100111010001011010111001101101001011110100110010101110100100000010000000110011001101111011011100111010001010011011010010111101001100101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110011011101000111100101101100011001010111110011010101001101010100110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100001011000100110110001100101010000001100011011011000110000101110011011100110111101010001001110000011000010110011101100101010000001110100011000010110001001101100011001010100011001101111011011100111010001010011011010010111101001100101010001001000000111001101110100011110010110110001100101011110101000100111000001100001011001000110010001101001011011100110011101011010111010001101111011100000111010010000001100000111000001111000011101101000000111011101101001011001000111010001101000011101001000000110001011000001100000100101011101101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110001001101111011001000111100101111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000111001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011100110111010001111001011011000110010101111010100010011000100110111101110010011001000110010101110010010110101110011011101000111100101101100011001010111010010000001110011011011110110110001101001011001000100010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011000010110001001101100011001010100000011000110110110001100001011100110111001101111010100010011101000110000101100010011011000110010101000000111010001100001011000100110110001100101010110101100010011011110111001001100100011001010111001001100101011001000100000011101000110000101100010011011000110010101000110011011110110111001110100010100110110100101111010011001010100010010000001110011011101000111100101101100011001010111101010001001110111011010010110010001110100011010000111010010000001100010110000011000001001010100010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001110010010000001110011011101000111100101101100011001010111101010001001101000011001010110100101100111011010000111010001110100100000011000101100000110010101101101011101101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001100011011011110110110001110011011100000110000101101110011110101000100110001010001001000000111001101110100011110010110110001100101011110101000100111011101101001011001000111010001101000011101001000000110001011011001001010100000010000101101001011011010111000001101111011100100111010001100001011011100111010001110110100000011000100110111101110010011001000110010101110010010110101110010011010010110011101101000011101000101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001110110100000011000100110111101110010011001000110010101110010010110101100010011011110111010001110100011011110110110101011010111011101101001011001000111010001101000011101001000000110000011100000111100001000000100001011010010110110101110000011011110111001001110100011000010110111001110100011101101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011010010110110101100111010000001110011011101000111100101101100011001010111101010001001110111011010010110010001110100011010000111010010000001110010110010101101101011101101000000110100001100101011010010110011101101000011101000111010010000001110010110010101101101011101101000100100000011100110111001001100011011110101000100111111001011110100001101101111011011100111010001100101011011100111010001011110101001101100011011010000100110001101111011001110110111101110011010111101000000010100001010110011010010110010101110111010000100110000101100111010111001110011011000110110100001101111011011110110110001001100011011110110011101101111010100110111010001110010011010010110111001100111010000001111110111111010000001000100100111001100001011010010111010001110011010011000110111101100111011011110101110011100000110111001100111010001001010010100010010000001100011011011000110000101110011011100110111101010001001101001011011010110011101011010111001001100101011100110111000001101111011011100111001101101001011101100110010101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011000110110111101101100011100110111000001100001011011100111101010001001000000010100001100011011011110110110001010011011100000110000101101110010110101100010101001010001001000000111001101110100011110010110110001100101011110101000100111011101101001011001000111010001101000011101001000000110110011011101001010100000010000101101001011011010111000001101111011100100111010001100001011011100111010001110110100000011000100110111101110010011001000110010101110010010110101101100011001010110011001110100010110101110111011010010110010001110100011010000111010010000001100000111000001111000010000001000010110100101101101011100000110111101110010011101000110000101101110011101000111011010000001100010011011110111001001100100011001010111001001011010111001001101001011001110110100001110100010110101110111011010010110010001110100011010000111010010000001100000111000001111000010000001000010110100101101101011100000110111101110010011101000110000101101110011101000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111010001100101011110000111010001011010110000101101100011010010110011101101110011101001000000110001101100101011011100111010001100101011100100111011010000001100010011011110111001001100100011001010111001001011010110001001101111011101000111010001101111011011010101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001110110100000011100000110000101100100011001000110100101101110011001110101101011101000110111101110000011101001000000110001011001010110110101110110100000011001100110111101101110011101000101101011100110110100101111010011001010111010010000001100010110010011100000111100001000010110100101101101011100000110111101110010011101000110000101101110011101000100010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110011011100000110000101101110010000001110011011101000111100101101100011001010111101010001001110100011001010111100001110100010110101110100011100100110000101101110011100110110011001101111011100100110110101110100100000011101010111000001110000011001010111001001100011011000010111001101100101011101101000000111011101101001011001000111010001101000011101001000000110001011000001100000100101011101101000000110011001101111011011100111010001011010111011101100101011010010110011101101000011101000111010010000001110000110000011000001110110100000011001100110111101101110011101000101101011100110110100101111010011001010111010010000001101100011000010111001001100111011001010111011011011000110010101110100011101000110010101110010010110101110011011100000110000101100011011010010110111001100111011101001000000110100011100000111100001110110100000011101000110010101111000011101000101101011000010110110001101001011001110110111001110100100000011000110110010101101110011101000110010101110010010001001111100100000010000000101011001101001011001010111011101000010011000010110011101011100101001101100011011010000110111101101111011011000100111001100001011011010110010101111000101111011100110111000001100001011011100111110011110001100010011100100100000010111101111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111001101110000011000010110111001000000111001101110100011110010110110001100101011110101000100111010001100101011110000111010001011010111010001110010011000010110111001110011011001100110111101110010011011010111010010000001110101011100000111000001100101011100100110001101100001011100110110010101110110100000011011000110010101110100011101000110010101110010010110101110011011100000110000101100011011010010110111001100111011101001000000110010011100000111100001110110100000011000110110111101101100011011110111001001110100100000011000100110110001110101011001010111011010000001100110011011110110111001110100010110101110111011001010110100101100111011010000111010001110100100000011011101100000110000010001001111100100110101001111010101000101010001001111011101001000000100000001010110011010010110010101110111010000100110000101100111010111001101101011011110111010001110100011011110111100010111101110011011100000110000101101110011111001000000111100011000100111001001000000101111011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110011011100000110000101101110011111001111000111001101110100011100100110111101101110011001110111110010000010110010001100100011100100110010101110011011100110111010010000001111000101111011100110111010001110010011011110110111001100111011111001000000010101100110100101100101011101110100001001100001011001110101110010100110110001101101000011011110110111101101100010000010110010001100100011100100110010101110011011100110111100010111101110011011100000110000101101110011111001111000110001001110010010000001011110111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011000100111001001000000101111011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110011011100000110000101101110010000001110011011101000111100101101100011001010111101010001001100110011011110110111001110100010110101110111011001010110100101100111011010000111010001110100100000011000100110111101101100011001000100010011111001010010010001010101000001001111010100100101010001000000101001101001000010001010100010101010100010000001000110010011110101001001000000111100011100110111000001100001011011100100000011100110111010001111001011011000110010101111010100010011101000110010101111000011101000101101011101000111001001100001011011100111001101100110011011110111001001101101011101001000000111010101110000011100000110010101110010011000110110000101110011011001010100010011111001000000011101000110010101110010011011010111100010111101110011011100000110000101101110011111001000000101010001000101010100100100110101000000100000001010000110000101100011011000010110010001100101011011010110100101100011010100110110010101110011011100110110100101101111011011100100000011111101111110100000010001001001110011010010110110001000100101001010000001000001010000110100000101000100010001010100110101001001010000110100000010100110100010101010011010100110100100101001111010011100111100010111101110011011100000110000101101110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110110011000010111001001000000111000001110011011100110111000001101111011100100111010001000000111101010000001000100100010011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011010010110011001000000101000011100110111010001110010011010010110111001100111010111001001001011100110100111001110101011011000110110001001111011100100101011101101000011010010111010001100101010100110111000001100001011000110110010101010000110010101100001011000110110100001010011011101000111010101100100011001010110111001110100010111001000110011010010111001001110011011101000101000010100101011100101001101110100011101010110010001100101011011100111010001011100101000001100001011100110111001101110000011011110111001001110100010100101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100000111001101110011011100000110111101110010011101000100000011110101000000100010011011100110111101011010111000001100001011100110111001101110000011011110111001001110100010111001101010011100000110011101000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010100000011010010110011001000000101000010000101010000101001101111001011100110111010001100101011011010101110010010010100111101011100100011001101001011011000110010101011100100010101111000011010010111001101110100011100110101000010100000110000101110100011010000101110010000110110111101101101011000100110100101101110011001010101000010100110110010101110010011101100110010101110010010111001001101011000010111000001010000011000010111010001101000010100001000100111111001011110101000001100001011100110111001101110000011011110111001001110100010111101000100101001010110001000000110010101100001011000110110100001010011011101000111010101100100011001010110111001110100010111001000110011010010111001001110011011101000101000010100101011100101001101110100011101010110010001100101011011100111010001011100101000001100001011100110111001101110000011011110111001001110100010100101010010101001010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111000001110011011100110111000001101111011100100111010001000000111101010000001000100110111001101111010110101110000011000010111001101110011011100000110111101110010011101000101110011010100111000001100111010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111000001110011011100110111000001101111011100100111010001000000111101010000001100101011000010110001101101000010100110111010001110101011001000110010101101110011101000101110010001100110100101110010011100110111010001010000101001010111001010011011101000111010101100100011001010110111001110100010111001010000011000010111001101110011011100000110111101110010011101000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011000110110111101101100011100110111000001100001011011100111101010001001100010100010010000001110011011101000111100101101100011001010111101010001001110111011010010110010001110100011010000111010010000001100010110111010010101000000100001011010010110110101110000011011110111001001110100011000010110111001110100011101101000000110001001101111011100100110010001100101011100100101101011011000110010101100110011101000101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001110110100000011000100110111101110010011001000110010101110010010110101100010011011110111010001110100011011110110110101011010111011101101001011001000111010001101000011101001000000110000011100000111100001000000100001011010010110110101110000011011110111001001110100011000010110111001110100011101101000000111010001100101011110000111010001011010110000101101100011010010110011101101110011101001000000111001001101001011001110110100001110100010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000110100101101101011001110100000011100110111010001111001011011000110010101111010100010011101110110100101100100011101000110100001110100100000011100101100101011011010111011010000001101000011001010110100101100111011010000111010001110100100000011100101100101011011010111011010001001000000111001101110010011000110111101010001001111110010111101010000011000010111001101110011011100000110111101110010011101000101111010000000111000001110011011100110111000001101111011100100111010001000100100000011000110110110001100001011100110111001101111010100010011010010110110101100111010110101110010011001010111001101110000011011110110111001110011011010010111011001100101010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001100100011111001101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011100100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000000111101101101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111101011110101001101100101011100000110000101110010011000010111010001100101010000001100011011000010110110001100011011101010110110001100001011101000110100101101111011011100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011011100110111101001111011001100101001101110101011000100110101001100101011000110111010001001111011001100110011001100101011100100110010101100100010000001111010100000011001010110000101100011011010000101001101110100011101010110010001100101011011100111010001011100100001101101111011101010110111001110100010100001010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101111011101100110010101110010011000010110110001101100010101000110111101110100011000010110110001010011011000110110111101110010011001010100000011110101000000110010101100001011000110110100001010011011101000111010101100100011001010110111001110100010111001010011011101010110110101010000111001101000000111101011111001000000111001101011100100001101100001011000101000000101011010000001110011010111001000011011000010110010010000001010110100000011100110101110010000110110000101100110100000010101101000000111001101011100100010101111000011000010110110101110011010100101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011011110111011001100101011100100110000101101100011011000100000101110110011001010111001001100001011001110110010101010011011000110110111101110010011001010100000011110101000000110111101110110011001010111001001100001011011000110110001010100011011110111010001100001011011000101001101100011011011110111001001100101010000001011110100000011011100110111101001111011001100101001101110101011000100110101001100101011000110111010001001111011001100110011001100101011100100110010101100100011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000101111010111101000010011001010110110001101111011101110100000011010000110000101101110011001000110110001100101011100110100000011000110110000101110011011001010100000011011110110011001000000110110101101111011100100110010101000000111010001101000011000010110111001000000110111101101110011001010100000011100110111010001110101011001000110010101101110011101000100000011010000110000101110110011010010110111001100111010000001110100011010000110010101000000100111010101000110111101110100011000010110110001000000101001101100011011011110111001001100101010011101000000101000010000100111001001100001011000110110101101100101011101000110100101101110011001110101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101001011001100100000010100001101100011000010111001101110100010011110111011001100101011100100110000101101100011011000100000101110110011001010111001001100001011001110110010101010011011000110110111101110010011001010100000011110101111010100000011011110111011001100101011100100110000101101100011011000100000101110110011001010111001001100001011001110110010101010011011000110110111101110010011001010100000010011001001100100000011011110111011001100101011100100110000101101100011011000100000101110110011001010111001001100001011001110110010101010011011000110110111101110010011001010100000011111001000000110000010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000110110110001100001011100110111001101010000011011110111001101101001011101000110100101101111011011100100001101101111011101010110111001110100011001010111001001000000111101010000001101100011000010111001101110100010000110110110001100001011100110111001101010000011011110111001101101001011101000110100101101111011011100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010001101001011001100110011001100101011100100110010101101110011000110110010101000000111101010000001100100011010010110011001100110011001010111001001100101011011100110001101100101010000001010110100000011000101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101001011001100100000010100001100100011010010110011001100110011001010111001001100101011011100110001101100101010000001111010111101010000001100010101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000110110111101110101011011100111010001100101011100100100000011110101000000110001101101100011000010111001101110011010100000110111101110011011010010111010001101001011011110110111001000011011011110111010101101110011101000110010101110010010000001010110100000011000101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010001101001011001100110011001100101011100100110010101101110011000110110010101000000111101010000001100000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010100000011010010110011001000000101000011001000110100101100110011001100110010101110010011001010110111001100011011001010100000011110101111010100000011001001010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000110110110001100001011100110111001101010000011011110111001101101001011101000110100101101111011011100100001101101111011101010110111001110100011001010111001001000000111101010000001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000110110111101110101011011100111010001100101011100100100000010101101000000110010011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100100011010010110011001100110011001010111001001100101011011100110001101100101010000001111010100000011000001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101000000110100101100110010000001010000110010001101001011001100110011001100101011100100110010101101110011000110110010101000000111101011110101000000110011010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110001101101100011000010111001101110011010100000110111101110011011010010111010001101001011011110110111001000011011011110111010101101110011101000110010101110010010000001111010100000011000110110110001100001011100110111001101010000011011110111001101101001011101000110100101101111011011100100001101101111011101010110111001110100011001010111001001000000101011010000001100110111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001000110100101100110011001100110010101110010011001010110111001100011011001010100000011110101000000110000011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001100100011010010110011001100110011001010111001001100101011011100110001101100101010000001111010111101010000001101000101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000110110111101110101011011100111010001100101011100100100000011110101000000110001101101100011000010111001101110011010100000110111101110011011010010111010001101001011011110110111001000011011011110111010101101110011101000110010101110010010000001010110100000011010001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010001101001011001100110011001100101011100100110010101101110011000110110010101000000111101010000001100000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010100000011010010110011001000000101000011001000110100101100110011001100110010101110010011001010110111001100011011001010100000011110101111010100000011010101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000110110110001100001011100110111001101010000011011110111001101101001011101000110100101101111011011100100001101101111011101010110111001110100011001010111001001000000111101010000001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000110110111101110101011011100111010001100101011100100100000010101101000000110101011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100100011010010110011001100110011001010111001001100101011011100110001101100101010000001111010100000011000001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101100011000010111001101110100010011110111011001100101011100100110000101101100011011000100000101110110011001010111001001100001011001110110010101010011011000110110111101110010011001010100000011110101000000110111101110110011001010111001001100001011011000110110001000001011101100110010101110010011000010110011101100101010100110110001101101111011100100110010101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111101011110100010101101110011001000110101010011010101001101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000001111010100000011000110110110001100001011100110111001101010000011011110111001101101001011101000110100101101111011011100100001101101111011101010110111001110100011001010111001001011100101010001101111010100110111010001110010011010010110111001100111010100001010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101001011001100100000010100001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000110110111101110101011011100111010001100101011100100100000011111001000000110000010000001001100100110010000001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000110110111101110101011011100111010001100101011100100100000011110001000000110010011000101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101001011001100100000010100001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000001111010111101010000001000100110001010001001010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000110110110001100001011100110111001101010000011011110111001101101001011101000110100101101111011011100100000011110101000000100010011000101110011011101000100010011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000001111010111101010000001000100110010010001001010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000110110110001100001011100110111001101010000011011110111001101101001011101000110100101101111011011100100000011110101000000100010011001001101110011001000100010011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000001111010111101010000001000100110011010001001010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000110110110001100001011100110111001101010000011011110111001101101001011101000110100101101111011011100100000011110101000000100010011001101110010011001000100010011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000001111010100000011000110110110001100001011100110111001101010000011011110111001101101001011101000110100101101111011011100100000010101101000000100010011101000110100001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101000000110100101100110010000001010000110001101101100011000010111001101110011010100000110111101110011011010010111010001101001011011110110111001000011011011110111010101101110011101000110010101110010010000001111100100000011001001100000101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011011000110000101110011011101000100010001101001011001110110100101110100010000001111010100000011000110110110001100001011100110111001101010000011011110111001101101001011101000110100101101111011011100101101101100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010111001001100011001010110111001100111011101000110100001000000101101010000001100010101110101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101001011001100100000010100001101100011000010111001101110100010001000110100101100111011010010111010001000000111101011110101000000100111011000101001110101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000001111010100000011000110110110001100001011100110111001101010000011011110111001101101001011101000110100101101111011011100100000010101101000000100010011100110111010001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010100000011010010110011001000000101000011011000110000101110011011101000100010001101001011001110110100101110100010000001111010111101010000001001110110010010011101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000110110110001100001011100110111001101010000011011110111001101101001011101000110100101101111011011100100000011110101000000110001101101100011000010111001101110011010100000110111101110011011010010111010001101001011011110110111001000000101011010000001000100110111001100100010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101000000110100101100110010000001010000110110001100001011100110111010001000100011010010110011101101001011101000100000011110101111010100000010011101100110100111010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110001101101100011000010111001101110011010100000110111101110011011010010111010001101001011011110110111001000000111101010000001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000001010110100000010001001110010011001000100010011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100011011011000110000101110011011100110101000001101111011100110110100101110100011010010110111101101110010000001111010100000011000110110110001100001011100110111001101010000011011110111001101101001011101000110100101101111011011100100000010101101000000100010011101000110100001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001101111011101100110010101110010011000010110110001000111011100100110000101100100011001010101011001100001011011000111010101100101010000001111010100000010001110110010101110100010001110111001001100001011001000110010101010000110111101110110011001010111001001100001011011000110110001000001011101100110010101110010011000010110011101100101010100110110001101101111011100100110010101010010111011011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011011100110010101110111010100110110001101101000011011110110111101101100010010000110010101100001011001000100001101101111011011010110110101100101011011100111010001110011010000001111010100000010001001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110110011000010111001001000000110111001100101011101110101010001100101011000010110001101101000011001010111001001000011011011110110110101101101011001010110111001110100011100110100000011110101000000100010010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011010010110011001000000101000011011110111011001100101011100100110000101101100010001110111001001100001011001000110010101010110011000010110110001110101011001010100000011110101111010100000010011101000110010011101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101110011001010111011101010011011000110110100001101111011011110110110001001000011001010110000101100100010000110110111101101101011011010110010101101110011101000111001101000000111101010000001000100101100101101111011101010100000011011100110010101100101011001000100000011101000110111101000000111011101100001011010110110010101000000111010101110000010110001000000111000001101100011001010110000101110011011001010101110010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101110011001010111011101010100011001010110000101100011011010000110010101110010010000110110111101101101011011010110010101101110011101000111001101000000111101010000001000100101011001100101011100100111100101000000101000001101111011011110111001001000000111001001100101011100110111010101101100011101000101100010000001100010011101010110001101101011011011000110010101000000111010101110000010000001101110011001010111100001110100010000001110100011001010111001001101101010000001110000011011000110010101100001011100110110010101011100100010011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101000000110100101100110010000001010000110111101110110011001010111001001100001011011000100011101110010011000010110010001100101010101100110000101101100011101010110010101000000111101011110101000000100111010001010100111010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011011100110010101110111010100110110001101101000011011110110111101101100010010000110010101100001011001000100001101101111011011010110110101100101011011100111010001110011010000001111010100000010001001000001010000001100110011000010110100101110010010000001110000011001010111001001100110011011110111001001101101011000010110111001100011011001010101100010000001110111011000010110101101100101010000001110101011100000100000011100000110110001100101011000010111001101100101010111001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110111001100101011101110101010001100101011000010110001101101000011001010111001001000011011011110110110101101101011001010110111001110100011100110100000011110101000000100010010100000110111101101111011100100100000010100000110010101110010011001100110111101110010011011010110000101101110011000110110010101000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001101111011101100110010101110010011000010110110001000111011100100110000101100100011001010101011001100001011011000111010101100101010000001111010111101010000001001110100010001001110101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110111001100101011101110101001101100011011010000110111101101111011011000100100001100101011000010110010001000011011011110110110101101101011001010110111001110100011100110100000011110101000000100010010100000111010101110100010000001101101011011110111001001100101010000001100101011001100110011001101111011100100111010001000000110100101101110010000001111001011011110111010101110010010000001110111011001010110000101101011010000001110011011101010110001001101010011001010110001101110100010110001000000111000001101100011001010110000101110011011001010101110010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101110011001010111011101010100011001010110000101100011011010000110010101110010010000110110111101101101011011010110010101101110011101000111001101000000111101010000001000100101100101101111011101010100000011011100110010101100101011001000100000011101000110111101000000111000001110101011101000100000011011010110111101110010011001010100000011001010110011001100110011011110111001001110100010111001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001101111011101100110010101110010011000010110110001000111011100100110000101100100011001010101011001100001011011000111010101100101010000001111010111101010000001001110100001101001110101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110111001100101011101110101001101100011011010000110111101101111011011000100100001100101011000010110010001000011011011110110110101101101011001010110111001110100011100110100000011110101000000100010010001110110111101101111011001000101100010000001100010011101010111010001000000111000001110101011101000100000011011010110111101110010011001010100000011001010110011001100110011011110111001001110100010000001101110011001010111100001110100010000001110100011001010111001001101101010110001000000111000001101100011001010110000101110011011001010101110010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101110011001010111011101010100011001010110000101100011011010000110010101110010010000110110111101101101011011010110010101101110011101000111001101000000111101010000001000100100100101110100011100110100000011000010100000011001110110111101101111011001000100000011100100110010101110011011101010110110001110100010110001000000110001001110101011101000100000011110010110111101110101010000001100011011000010110111001000000111001101110100011100100110100101110110011001010100000011101000110111101000000110010001101111010000001100010011001010111010001110100011001010111001001011100100010011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101000000110100101100110010000001010000110111101110110011001010111001001100001011011000100011101110010011000010110010001100101010101100110000101101100011101010110010101000000111101011110101000000100111010000100100111010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011011100110010101110111010100110110001101101000011011110110111101101100010010000110010101100001011001000100001101101111011011010110110101100101011011100111010001110011010000001111010100000010001001000001010000001110110011001010111001001111001010000001100111011011110110111101100100010000001110010011001010111001101110101011011000111010001011000100000011010110110010101100101011100000100000011010010111010001000000111010101110000010111001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110111001100101011101110101010001100101011000010110001101101000011001010111001001000011011011110110110101101101011001010110111001110100011100110100000011110101000000100010010001110110111101101111011001000100000010100000110010101110010011001100110111101110010011011010110000101101110011000110110010101000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001101111011101100110010101110010011000010110110001000111011100100110000101100100011001010101011001100001011011000111010101100101010000001111010111101010000001001110100000101001110101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110111001100101011101110101001101100011011010000110111101101111011011000100100001100101011000010110010001000011011011110110110101101101011001010110111001110100011100110100000011110101000000100010010000010110111001000000110010101111000011000110110010101101100011011000110010101101110011101000100000011100000110010101110010011001100110111101110010011011010110000101101110011000110110010101011000100000011010110110010101100101011100000100000011010010111010001000000111010101110000010111001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110111001100101011101110101010001100101011000010110001101101000011001010111001001000011011011110110110101101101011001010110111001110100011100110100000011110101000000100010010000010100000011000100111001001101001011011000110110001101001011000010110111001110100010000001110111011011110111001001101011010110001000000110101101100101011001010111000001000000110100101110100010000001110101011100000101110010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101001011001100100000010100001110011011101000111001001101001011011100110011101011100100100101110011010011100111010101101100011011000100111101110010010101110110100001101001011101000110010101010011011100000110000101100011011001010101000011100110110001101101000011011110110111101101100010010000110010101100001011001000100001101101111011011010110110101100101011011100111010001110011010100101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110011011000110110100001101111011011110110110001001000011001010110000101100100010000110110111101101101011011010110010101101110011101000111001101000000111101010000001101110011001010111011101010011011000110110100001101111011011110110110001001000011001010110000101100100010000110110111101101101011011010110010101101110011101000111001101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011010010110011001000000101000011100110111010001110010011010010110111001100111010111001001001011100110100111001110101011011000110110001001111011100100101011101101000011010010111010001100101010100110111000001100001011000110110010101010000111010001100101011000010110001101101000011001010111001001000011011011110110110101101101011001010110111001110100011100110101001010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101000110010101100001011000110110100001100101011100100100001101101111011011010110110101100101011011100111010001110011010000001111010100000011011100110010101110111010101000110010101100001011000110110100001100101011100100100001101101111011011010110110101100101011011100111010001110011011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001110010010000001110011011101000111100101101100011001010111101010001001100010011011110111001001100100011001010111001001011010111001101110100011110010110110001100101011101001000000110111001101111011011100110010101110110100000011101110110100101100100011101000110100001110100100000011000101100000110000010010101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001100011011011110110110001110011011100000110000101101110011110101000100100000001010000110001101101111011011000101001101110000011000010110111001000000101011010000001100010101001010001001000000111001101110100011110010110110001100101011110101000100111011101101001011001000111010001101000011101001000000110001011000001100000100101010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100001011000100110110001100101010000001100011011011000110000101110011011100110111101010001001110100011000010110001001101100011001010100010010000001110011011101000111100101101100011001010111101010001001110111011010010110010001110100011010000111010010000001100010110000011000001001010100010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001110010010000001110011011101000111100101101100011001010111101010001001100010011011110111001001100100011001010111001001011010111011101101001011001000111010001101000011101001000000110000011100000111100001000000100001011010010110110101110000011011110111001001110100011000010110111001110100010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011100110111010001111001011011000110010101111010100010011101110110100101100100011101000110100001110100100000011010001100100100101010000001000010110100101101101011100000110111101110010011101000110000101101110011101000111011010000001100010011011110111001001100100011001010111001001011010111011101101001011001000111010001101000011101001000000110000011100000111100001000000100001011010010110110101110000011011110111001001110100011000010110111001110100010001001000000110001101101100011000010111001101110011011110101000100111011101110010011000010111000001000100100000011000110110111101101100011100110111000001100001011011100111101010001001100100100010011111001111000111001101110100011100100110111101101110011001110111110010011100110000101101101011001010111010010000001111000101111011100110111010001110010011011110110111001100111011111001111000111001101110000011000010110111001000000111001101110100011110010110110001100101011110101000100111010001100101011110000111010001011010111010001110010011000010110111001110011011001100110111101110010011011010111010010000001110101011100000111000001100101011100100110001101100001011100110110010101110110100000011100000110000101100100011001000110100101101110011001110101101011011000110010101100110011101000111010010000001100010110010101101101010001001111100100000001100101011000010110001101101000010100110111010001110101011001000110010101101110011101000101110010001100110100101110010011100110111010001010000101001010111001010011011101000111010101100100011001010110111001110100010111001010101011100110110010101110010010111001000110011101010110110001101100010011100110000101101101011001010100000011110001011110111001101110000011000010110111001111100111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001110011011101000111100101101100011001010111101010001001110111011010010110010001110100011010000111010010000001100100110001010010101000000100001011010010110110101110000011011110111001001110100011000010110111001110100011101101000000110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001110110100010011111001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011100110111010001111001011011000110010101111010100010011101110110100101100100011101000110100001110100100000011100101001010100000010000101101001011011010111000001101111011100100111010001100001011011100111010001110110100000011000100110111101110010011001000110010101110010010110101110111011010010110010001110100011010000111010010000001100000111000001111000010000001000010110100101101101011100000110111101110010011101000110000101101110011101000111011010001001111100111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001110011011101000111100101101100011001010111101010001001110111011010010110010001110100011010000111010010000001100100110000010010101000000100001011010010110110101110000011011110111001001110100011000010110111001110100011101101000000110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001110110100010011111001000110011010010110111001100001011011000100000010001110111001001100001011001000110010101110100100000011110001110011011100000110000101101110010000001110011011101000111100101101100011001010111101010001001110000011000010110010001100100011010010110111001100111010110101101100011001010110011001110100011101001000000110001011001010110110101000100111110010000000110111101110110011001010111001001100001011011000100011101110010011000010110010001100101010101100110000101101100011101010110010101111000101111011100110111000001100001011011100111110011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000111001101110100011110010110110001100101011110101000100111011101101001011001000111010001101000011101001000000111000010010101000000100001011010010110110101110000011011110111001001110100011000010110111001110100011101101000000110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001000100111110011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000111001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000111001001000000111001101110100011110010110110001100101011110101000100110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001110110100010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000111001101110100011110010110110001100101011110101000100110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001110110100010011111001111000111001101110100011100100110111101101110011001110111110010000110110110001100001011100110111001101110100100000011110001011110111001101110100011100100110111101101110011001110111110011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000111001101110100011110010110110001100101011110101000100110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001110110100010011111001111000111001101110000011000010110111001000000111001101110100011110010110110001100101011110101000100111010001100101011110000111010001011010111010001110010011000010110111001110011011001100110111101110010011011010111010010000001110101011100000111000001100101011100100110001101100001011100110110010101000100111110010000000110010101100001011000110110100001010011011101000111010101100100011001010110111001110100010111001010100011011110100110001101001011100110111010001010000101001010111001000110011010010111001001110011011101000101000010100101011100100001101101100011000010111001101110011010000010111001001101101010111001000011011011000110000101110011011100110111001101011100100001101101100011000010111001101110011010011000110000101100010011001010110110001000000011001010110000101100011011010000101001101110100011101010110010001100101011011100111010001011100101010001101111010011000110100101110011011101000101000010100101011100100011001101001011100100111001101110100010100001010010101110010000110110110001100001011100110111001101000001011100100110110101011100100000101110010011011010100111001100001011011010110010101111000101111011100110111000001100001011011100111110011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000111001101110100011110010110110001100101011110101000100110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001000100100000011000110110110001100001011100110111001101111010100010011101110111001001100001011100000100010010000001100011011011110110110001110011011100000110000101101110011110101000100110010010001001111100111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001110011011101000111100101101100011001010111101010001001100010011011110111001001100100011001010111001001011010111011101101001011001000111010001101000011101001000000110000011100000111100001000000100001011010010110110101110000011011110111001001110100011000010110111001110100011101101000100111110011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000111001101110100011110010110110001100101011110101000100110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001000100111110011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000111001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000111001001000000111001101110100011110010110110001100101011110101000100110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001100011011011000110000101110011011100110111101010001001110111011100100110000101110000010001001000000111001101110100011110010110110001100101011110101000100110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001110110100010011111001111000111001101110100011100100110111101101110011001110111110010000010110010001101101011010010111001101110011011010010110111101101110010000001001110011011110111010011110001011110111001101110100011100100110111101101110011001110111110011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000111001101110100011110010110110001100101011110101000100110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001000100111110010000000110010101100001011000110110100001010011011101000111010101100100011001010110111001110100010111001000110011010010111001001110011011101000101000010100101011100101001101110100011101010110010001100101011011100111010001011100100100101100100010011100110111101111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011100110111010001111001011011000110010101111010100010011000100110111101110010011001000110010101110010010110101110111011010010110010001110100011010000111010010000001100000111000001111000010000001000010110100101101101011100000110111101110010011101000110000101101110011101000100010011111001010100011011110111010001100001011011000100000010100110110001101101111011100100110010101110100100000011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000111001101110100011110010110110001100101011110101000100110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001000100111110010000000100110101100001011101000110100001011100101001001101111011101010110111001100100010100001101111011101100110010101110010011000010110110001101100010101000110111101110100011000010110110001010011011000110110111101110010011001010101100010000001100010101001011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000111001101110100011110010110110001100101011110101000100110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001000100111110010000010101010001010100010001010100111001000100010000010100111001000011010001010111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001110011011101000111100101101100011001010111101010001001100010011011110111001001100100011001010111001001011010111011101101001011001000111010001101000011101001000000110000011100000111100001000000100001011010010110110101110000011011110111001001110100011000010110111001110100010001001111100111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001110010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001110010010000001110011011101000111100101101100011001010111101010001001100010011011110111001001100100011001010111001001011010111011101101001011001000111010001101000011101001000000110000011100000111100001000000100001011010010110110101110000011011110111001001110100011000010110111001110100010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011100110111010001111001011011000110010101111010100010011000100110111101110010011001000110010101110010010110101110111011010010110010001110100011010000111010010000001100000111000001111000010000001000010110100101101101011100000110111101110010011101000110000101101110011101000100010010000001100011011011000110000101110011011100110111101010001001110111011100100110000101110000010001001111100111100011100110111010001110010011011110110111001100111011111001000001011000110110000101100100011001010110110101101001011000110100000010110010110010101100001011100100111010010000001111000101111011100110111010001110010011011110110111001100111011111001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011100110111010001111001011011000110010101111010100010011000100110111101110010011001000110010101110010010110101110111011010010110010001110100011010000111010010000001100000111000001111000010000001000010110100101101101011100000110111101110010011101000110000101101110011101000100010011111001000000010100001100001011000110110000101100100011001010110110101101001011000110101001101100101011100110111001101101001011011110110111001000000111111011111101000000100010010011100110111101110100010000001110011011001010111010001000100101001011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000111001101110100011110010110110001100101011110101000100110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001000100111110010001100110100101101110011000010110110001000000100000101110110011001010111001001100001011001110110010101110100111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001110011011101000111100101101100011001010111101010001001100010011011110111001001100100011001010111001001011010111011101101001011001000111010001101000011101001000000110000011100000111100001000000100001011010010110110101110000011011110111001001110100011000010110111001110100010001001111100100000001001101011000010111010001101000010111001010010011011110111010101101110011001000101000011011110111011001100101011100100110000101101100011011000100000101110110011001010111001001100001011001110110010101010011011000110110111101110010011001010101100010000001100010101001011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000111001101110100011110010110110001100101011110101000100110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001000100100000011000110110110001100001011100110111001101111010100010011101110111001001100001011100000100010011111001000100011000010111100101010000111001101010010100000010100110110001101101000011011110110111101101100010000001001111011100000110010101101110011101001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011100110111010001111001011011000110010101111010100010011000100110111101110010011001000110010101110010010110101110111011010010110010001110100011010000111010010000001100000111000001111000010000001000010110100101101101011100000110111101110010011101000110000101101110011101000100010011111001000000010100001100001011101000111010001100101011011100110010001100001011011100110001101100101010101000110111101110100011000010110110001000000111101011110101000000110000010000001111100011111000100000011000010111010001110100011001010110111001100100011000010110111001100011011001010101010001101111011101000110000101101100010000001111010111101010000001101110011101010110110001101100010000001111110100000010001001011111010111110100010010000001110100100000011000010111010001110100011001010110111001100100011000010110111001100011011001010101010001101111011101000110000101101100010111001010100011011110101001101110100011100100110100101101110011001110101000010100101010010111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001110010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001110010010000001110011011101000111100101101100011001010111101010001001100010011011110111001001100100011001010111001001011010111011101101001011001000111010001101000011101001000000110000011100000111100001000000100001011010010110110101110000011011110111001001110100011000010110111001110100010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011100110111010001111001011011000110010101111010100010011000100110111101110010011001000110010101110010010110101110111011010010110010001110100011010000111010010000001100000111000001111000010000001000010110100101101101011100000110111101110010011101000110000101101110011101000100010011111001111000111001101110100011100100110111101101110011001110111110010101000110010101110010011011010111010011110001011110111001101110100011100100110111101101110011001110111110011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000111001101110100011110010110110001100101011110101000100110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001000100111110011110001110011011100000110000101101110010000001110011011101000111100101101100011001010111101010001001000100111110010000000111010001100101011100100110110101111000101111011100110111000001100001011011100111110011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000111001101110100011110010110110001100101011110101000100110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001000100111110010010000110100101100111011010000110010101110011011101000100000010000010111011001100101010111001000000110100101101110010000001000011011011000110000101110011011100110111010010000001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011100110111010001111001011011000110010101111010100010011000100110111101110010011001000110010101110010010110101110111011010010110010001110100011010000111010010000001100000111000001111000010000001000010110100101101101011100000110111101110010011101000110000101101110011101000100010011111001000000010011010110000101110100011010000101110010100100110111101110101011011100110010001010000110001101101100011000010111001101110011010010000110100101100111011010000110010101110011011101000100000101110110011001010111001001100001011001110110010101011000100000011000101010010111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001110011011101000111100101101100011001010111101010001001100010011011110111001001100100011001010111001001011010111011101101001011001000111010001101000011101001000000110000011100000111100001000000100001011010010110110101110000011011110111001001110100011000010110111001110100010001001111100100010001100001011110010101000011100110101001010000001010000011100100110010101110011011001010110111001110100011101001111000101111011101000110010001111100110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001110011011101000111100101101100011001010111101010001001100010011011110111001001100100011001010111001001011010111011101101001011001000111010001101000011101001000000110000011100000111100001000000100001011010010110110101110000011011110111001001110100011000010110111001110100010001001111100100000001010000111001101110100011101010110010001100101011011100111010001000001011101000111010001100101011011100110010001100001011011100110001101100101010000001111010111101010000001100000100000011111101000000100010010111110101111101000100100000011101001000000111001101110100011101010110010001100101011011100111010001000001011101000111010001100101011011100110010001100001011011100110001101100101010111001010100011011110101001101110100011100100110100101101110011001110101000010100101010010111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001110010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001110010010000001110011011101000111100101101100011001010111101010001001100010011011110111001001100100011001010111001001011010111011101101001011001000111010001101000011101001000000110000011100000111100001000000100001011010010110110101110000011011110111001001110100011000010110111001110100010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011100110111010001111001011011000110010101111010100010011000100110111101110010011001000110010101110010010110101110111011010010110010001110100011010000111010010000001100000111000001111000010000001000010110100101101101011100000110111101110010011101000110000101101110011101000100010011111001111000111001101110100011100100110111101101110011001110111110010011100110111101011100100000011010010110111001000000100001101101100011000010111001101110011011101001111000101111011100110111010001110010011011110110111001100111011111001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011100110111010001111001011011000110010101111010100010011000100110111101110010011001000110010101110010010110101110111011010010110010001110100011010000111010010000001100000111000001111000010000001000010110100101101101011100000110111101110010011101000110000101101110011101000100010011111001000000011011100110111101001111011001100101001101110100011101010110010001100101011011100111010001110011010010010110111001010100011010000110010101000001011100100110110101111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011100110111010001111001011011000110010101111010100010011000100110111101110010011001000110010101110010010110101110111011010010110010001110100011010000111010010000001100000111000001111000010000001000010110100101101101011100000110111101110010011101000110000101101110011101000100010010000001100011011011000110000101110011011100110111101010001001110111011100100110000101110000010001001111100100110001101111011101110110010101110011011101000100000010000010111011001100101010111001000000110100101101110010000001000011011011000110000101110011011100110111010010000001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011100110111010001111001011011000110010101111010100010011000100110111101110010011001000110010101110010010110101110111011010010110010001110100011010000111010010000001100000111000001111000010000001000010110100101101101011100000110111101110010011101000110000101101110011101000100010011111001000000010011010110000101110100011010000101110010100100110111101110101011011100110010001010000110001101101100011000010111001101110011010011000110111101110111011001010111001101110100010000010111011001100101011100100110000101100111011001010101100010000001100010101001011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000111001101110100011110010110110001100101011110101000100110001001101111011100100110010001100101011100100101101011101110110100101100100011101000110100001110100100000011000001110000011110000100000010000101101001011011010111000001101111011100100111010001100001011011100111010001000100111110010001000110000101111001010100001110011010100101000000100000101100010011100110110010101101110011101000111010010000001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011100110111010001111001011011000110010101111010100010011000100110111101110010011001000110010101110010010110101110111011010010110010001110100011010000111010010000001100000111000001111000010000001000010110100101101101011100000110111101110010011101000110000101101110011101000100010011111001000000010100001110011011101000111010101100100011001010110111001110100010000010110001001110011011001010110111001110100010000001111010111101010000001100000100000011111101000000100010010111110101111101000100100000011101001000000111001101110100011101010110010001100101011011100111010001000001011000100111001101100101011011100111010001000000111101011110101000000110000101110100011101000110010101101110011001000110000101101110011000110110010101010100011011110111010001100001011011000100000011111101000000100010010111110101111101000100100000011101001000000111001101110100011101010110010001100101011011100111010001000001011000100111001101100101011011100111010001011100101010001101111010100110111010001110010011010010110111001100111010100001010010101001011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000111001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110000101100010011011000110010101111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000111001001111100110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011100100100000011100110111010001111001011011000110010101111010100010011101110110100101100100011101000110100001110100100000011000101100000110000010010101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001100011011011110110110001110011011100000110000101101110011110101000100100000001010000110001101101111011011000101001101110000011000010110111001000000101011010000001100010101001010001001000000111001101110100011110010110110001100101011110101000100111011101101001011001000111010001101000011101001000000110001011000001100000100101010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100001011000100110110001100101010000001100011011011000110000101110011011100110111101010001001110100011000010110001001101100011001010100000011101000110000101100010011011000110010101011010110001001101111011100100110010001100101011100100110010101100100010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000111001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011010000100000011100100110111101110111011100110111000001100001011011100111101010001001100100100010010000001110011011101000111100101101100011001010111101010001001110000011000010110010001100100011010010110111001100111011101001000000110000011100000111100001110110100000011101110110100101100100011101000110100001110100100000011001001101110100101010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001010011011101010110001001101010011001010110001101110100010011101110011010000001001110011000010110110101100101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110100001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000000110100101100110010000001010000110001101100001011100110100001101101111011011100110011001101001011001110101110010000110110000101110011010000110110000101110100011001010110011101101111011100100111100101000000111101011110101000000100111001100001011010010111010001110011010111001000011011011110110110101101101011011110110111001011100101001101101000011000010111001001100101011001000101110010000110110000101110011010000110110000101110100011001010110011101101111011100100111100101011100101010001101000011100100110010101100101010000110100000101110011010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011010000100000011100100110111101110111011100110111000001100001011011100111101010001001100100100010010000001100011011011000110000101110011011100110111101010001001110010011011110111010001100001011101000110010101000100100000011100110111010001111001011011000110010101111010100010011100000110000101100100011001000110100101101110011001110111010010000001100000111000001111000011101101000000111011101101001011001000111010001101000011101001000000110101010010101110110100010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000110010001101001011101100111110010000001111000111001101110000011000010110111001111100100001101000001011000101000000101000010000000101000011000110110000101110011010000110110111101101110011001100110100101100111010111001010000011001010111001001000011011000010111001101010110011000010110110001110101011001010101001010010101010010111100010111101110011011100000110000101101110011111001111000101111011001000110100101110110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001101000011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011010000100000011100100110111101110111011100110111000001100001011011100111101010001001100100100010010000001100011011011000110000101110011011100110111101010001001110010011011110111010001100001011101000110010101000100100000011100110111010001111001011011000110010101111010100010011100000110000101100100011001000110100101101110011001110111010010000001100000111000001111000011101101000000111011101101001011001000111010001101000011101001000000110101010010101110110100010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000110010001101001011101100111110010000001111000111001101110000011000010110111001111100100001101000001011001001000000101000010000000101000011000110110000101110011010000110110111101101110011001100110100101100111010111001010000011001010111001001000011011000010111001101010110011000010110110001110101011001010101001010010101010010111100010111101110011011100000110000101101110011111001111000101111011001000110100101110110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001101000011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011010000100000011100100110111101110111011100110111000001100001011011100111101010001001100100100010010000001100011011011000110000101110011011100110111101010001001110010011011110111010001100001011101000110010101000100100000011100110111010001111001011011000110010101111010100010011100000110000101100100011001000110100101101110011001110111010010000001100000111000001111000011101101000000111011101101001011001000111010001101000011101001000000110101010010101110110100010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000110010001101001011101100111110010000001111000111001101110000011000010110111001111100100000010000110100000101100110111100010111101110011011100000110000101101110011111001111000111001101110000011000010110111001000000111001101110100011110010110110001100101011110101000100110011001101111011011100111010001011010111011101100101011010010110011101101000011101000111010010000001101110011011110111001001101101011000010110110001000100111110010100001000000010100001100011011000010111001101000011011011110110111001100110011010010110011101011100101000001100101011100100100001101100001011100110101011001100001011011000111010101100101010100101001010101001011110001011110111001101110000011000010110111001111100111100010111101100100011010010111011001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110100001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010100000011010010110011001000000101000011000110110000101110011010000110110111101101110011001100110100101100111010111001000011011000010111001101000011011000010111010001100101011001110110111101110010011110010100000011110101111010100000010011100110000101101001011101000111001101011100100001101101111011011010110110101101111011011100101110010100110110100001100001011100100110010101100100010111001000011011000010111001101000011011000010111010001100101011001110110111101110010011110010101110010101000111011101101111010000110100000101110011010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011010000100000011100100110111101110111011100110111000001100001011011100111101010001001100100100010010000001100011011011000110000101110011011100110111101010001001110010011011110111010001100001011101000110010101000100100000011100110111010001111001011011000110010101111010100010011100000110000101100100011001000110100101101110011001110111010010000001100000111000001111000011101101000000111011101101001011001000111010001101000011101001000000110101010010101110110100010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000110010001101001011101100111110010000001111000111001101110000011000010110111001111100100001101000001011000101000000101000010000000101000011000110110000101110011010000110110111101101110011001100110100101100111010111001010000011001010111001001000011011000010111001101010110011000010110110001110101011001010101001010010101010010111100010111101110011011100000110000101101110011111001111000101111011001000110100101110110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001101000011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011010000100000011100100110111101110111011100110111000001100001011011100111101010001001100100100010010000001100011011011000110000101110011011100110111101010001001110010011011110111010001100001011101000110010101000100100000011100110111010001111001011011000110010101111010100010011100000110000101100100011001000110100101101110011001110111010010000001100000111000001111000011101101000000111011101101001011001000111010001101000011101001000000110101010010101110110100010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000110010001101001011101100111110010000001111000111001101110000011000010110111001111100100001101000001011001001000000101000010000000101000011000110110000101110011010000110110111101101110011001100110100101100111010111001010000011001010111001001000011011000010111001101010110011000010110110001110101011001010101001010010101010010111100010111101110011011100000110000101101110011111001111000101111011001000110100101110110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001101000011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011010000100000011100100110111101110111011100110111000001100001011011100111101010001001100100100010010000001100011011011000110000101110011011100110111101010001001110010011011110111010001100001011101000110010101000100100000011100110111010001111001011011000110010101111010100010011100000110000101100100011001000110100101101110011001110111010010000001100000111000001111000011101101000000111011101101001011001000111010001101000011101001000000111000010010101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001100100011010010111011001111100100000011110001110011011100000110000101101110011111001000011010000010100000010100001000000011000110110000101110011010000110110111101101110011001100110100101100111010111001010000011001010111001001000011011000010111001101010110011000010110110001110101011001010100101010100101111000101111011100110111000001100001011011100111110011110001011110110010001101001011101100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011010000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011010000100000011100100110111101110111011100110111000001100001011011100111101010001001100100100010010000001100011011011000110000101110011011100110111101010001001110010011011110111010001100001011101000110010101000100100000011100110111010001111001011011000110010101111010100010011100000110000101100100011001000110100101101110011001110111010010000001100000111000001111000011101101000000111011101101001011001000111010001101000011101001000000110110010010101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011001000110100101110110011111001111000111001101110000011000010110111001000000111001101110100011110010110110001100101011110101000100110011001101111011011100111010001011010111011101100101011010010110011101101000011101000111010010000001100010011011110110110001100100010001001111100100010101111000011000010110110101000000101000010000000110010101111000011000010110110101110011010100110110001101101111011100100110010101001010101001011110001011110111001101110000011000010110111001111100111100010111101100100011010010111011001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001101000011111001101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011010000100000011100100110111101110111011100110111000001100001011011100111101010001001100100100010010000001100011011011000110000101110011011100110111101010001001110010011011110111010001100001011101000110010101000100100000011100110111010001111001011011000110010101111010100010011100000110000101100100011001000110100101101110011001110111010010000001100000111000001111000011101101000000111011101101001011001000111010001101000011101001000000110110010010101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011001000110100101110110011111001111000111001101110000011000010110111001000000111001101110100011110010110110001100101011110101000100110011001101111011011100111010001011010111011101100101011010010110011101101000011101000111010010000001100010011011110110110001100100010001001111100101010001101111011101000110000101101100010000001010000110001011000001100000100101010100101111000101111011100110111000001100001011011100111110011110001011110110010001101001011101100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110100001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011010000100000011100100110111101110111011100110111000001100001011011100111101010001001100100100010010000001100011011011000110000101110011011100110111101010001001110010011011110111010001100001011101000110010101000100100000011100110111010001111001011011000110010101111010100010011100000110000101100100011001000110100101101110011001110111010010000001100000111000001111000011101101000000111011101101001011001000111010001101000011101001000000110101010010101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011001000110100101110110011111001111000111001101110000011000010110111001111100100011101110010011000010110010001100101011110001011110111001101110000011000010110111001111100111100010111101100100011010010111011001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001101000011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110100001000000111001001101111011101110111001101110000011000010110111001111010100010011001001000100100000011000110110110001100001011100110111001101111010100010011100100110111101110100011000010111010001100101010001001000000111001101110100011110010110110001100101011110101000100111000001100001011001000110010001101001011011100110011101110100100000011000001110000011110000111011010000001110111011010010110010001110100011010000111010010000001101010100101010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000110010001101001011101100111110011110001110011011100000110000101101110011111001010000010011110101001101111000101111011100110111000001100001011011100111110011110001011110110010001101001011101100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110100001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011010000100000011100100110111101110111011100110111000001100001011011100111101010001001100100100010010000001100011011011000110000101110011011100110111101010001001110010011011110111010001100001011101000110010101000100100000011100110111010001111001011011000110010101111010100010011100000110000101100100011001000110100101101110011001110111010010000001100000111000001111000011101101000000111011101101001011001000111010001101000011101001000000110101010010101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011001000110100101110110011111001111000111001101110000011000010110111001111100100111101110101011101000100000010011110110011001111000101111011100110111000001100001011011100111110011110001011110110010001101001011101100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110100001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011010000100000011100100110111101110111011100110111000001100001011011100111101010001001100100100010010000001100011011011000110000101110011011100110111101010001001110010011011110111010001100001011101000110010101000100100000011100110111010001111001011011000110010101111010100010011100000110000101100100011001000110100101101110011001110111010010000001100000111000001111000011101101000000111011101101001011001000111010001101000011101001000000110110010010101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011001000110100101110110011111001111000111001101110000011000010110111001111100100110101101001011011100100000010100110110001101101111011100100110010101111000101111011100110111000001100001011011100111110011110001011110110010001101001011101100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110100001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011010000100000011100100110111101110111011100110111000001100001011011100111101010001001100100100010010000001100011011011000110000101110011011100110111101010001001110010011011110111010001100001011101000110010101000100100000011100110111010001111001011011000110010101111010100010011100000110000101100100011001000110100101101110011001110111010010000001100000111000001111000011101101000000111011101101001011001000111010001101000011101001000000110110010010101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011001000110100101110110011111001111000111001101110000011000010110111001111100100110101100001011110000100000010100110110001101101111011100100110010101111000101111011100110111000001100001011011100111110011110001011110110010001101001011101100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110100001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011010000100000011100100110111101110111011100110111000001100001011011100111101010001001100100100010010000001100011011011000110000101110011011100110111101010001001110010011011110111010001100001011101000110010101000100100000011100110111010001111001011011000110010101111010100010011100000110000101100100011001000110100101101110011001110111010010000001100000111000001111000011101101000000111011101101001011001000111010001101000011101001000000110111010010101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011001000110100101110110011111001111000111001101110000011000010110111001111100100001101101100011000010111001101110011010000001000001010101100100011101000000111100010111101110011011100000110000101101110011111001111000101111011001000110100101110110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011010000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001101000010000001110010011011110111011101110011011100000110000101101110011110101000100110010010001001000000110001101101111011011000111001101110000011000010110111001111010100010011001001000100100000011100110111010001111001011011000110010101111010100010011100000110000101100100011001000110100101101110011001110111010010000001100000111000001111000011101101000100111110010100100110010101101101011000010111001001101011011100110111100010111101110100011010000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001110010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100010011011110110010001111001011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011001110111001001101111011101010111000001000010011110010101010001100101011100100110110101000000111101010000001100101011000010110001101101000010100110111010001110101011001000110010101101110011101000101110010011110111001001100100011001010111001001000010011110010101000011000110100000011110101111100100000011000110101110010110010110010101100001011100100110110001111001010000010110111001100100010101000110010101110010011011010110110001111001010100110111010001100001011101000111010101110011010010010110010001010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011000110111010101110010011100100110010101101110011101000101010001100101011100100110110101000000111101010000001100111011100100110111101110101011100000100001001111001010101000110010101110010011011010101110010011000110000101110011011101000101000010100101011100101100101100101011000010111001001101100011110010100000101101110011001000101010001100101011100100110110101101100011110010101001101110100011000010111010001110101011100110101110010101000110010101110010011011010101001101110100011000010111010001110101011100110111011011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011010010110011001000000101000011000110111010101110010011100100110010101101110011101000101010001100101011100100110110101000000100001011110101000000101010001100101011100100110110101010011011101000110000101110100011101010111001101011100101010001101000011010010111001001100100010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011011110111001001100100011001010111001001010011011101000111010101100100011001010110111001110100010000001111010100000011001010110000101100011011010000101001101110100011101010110010001100101011011100111010001011100100111101110010011001000110010101110010010000100111100101010000110001101000000111101011111001000000110001101011100101001101110101011000100110101001100101011000110111010001001001011001000101001010111001010100011011110100110001101001011100110111010001010000101001011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100110011011110111001001100101011000010110001101101000010000001010000111011001100001011100100100000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001000000110100101101110010000001101111011100100110010001100101011100100101001101110100011101010110010001100101011011100111010001010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011100110111010101100010011010100110010101100011011101000101001101100011011011110111001001100101010100000110111101110011011010010111010001101001011011110110111001000000111101010000001100010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100010011110010111010001100101010000001101110011011110100111101100110010100110111010001110101011001000110010101101110011101000111001101001001011011100101010001101000011001010100000101110010011011010100111101100110011001100110010101110010011010010110111001100111010101000110100001100101010100110111010101100010011010100110010101100011011101000100000011110101000000110000011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011100110111010101100010011010100110010101100011011101000100110001101111011101110110010101110011011101000101001101100011011011110111001001100101010000001111010100000011000001011100110000011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011100110111010101100010011010100110010101100011011101000100100001101001011001110110100001100101011100110111010001010011011000110110111101110010011001010100000011110101000000110000010111001100000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110110011000010111001001000000111001101110101011000100110101001100101011000110111010001000001011101100110010101110010011000010110011101100101010100110110001101101111011100100110010101000000111101010000001100000101110011000001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000110111010101110010011100100110010101101110011101000101001101110100011101010110010001100101011011100111010001001001011001000100000011110101000000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001010011011101000111010101100100011001010110111001110100010010010110010001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001110100011001010110000101100011011010000110010101110010011100110100111001100001011011010110010101000000111101010000001000100100010011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011100100110010101101101011000010111001001101011011100110100000011110101000000100010010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111101011110100000011101100110000101110010010000001110011011101000111010101100100011001010110111001110100010100110111010101100010011010100110010101100011011101000101000001101111011100110110100101110100011010010110111101101110010000001111010100000011000101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011100100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000110001101101100011000010111001101110011011110101000100111011101110010011000010111000001000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000000100100001110100011011010110110001011100100010001101001011100110111000001101100011000010111100101000110011011110111001001010000110110101101111011001000110010101101100010010010111010001100101011011010100000011110101111100100000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001011100101001101110101011000100110101001100101011000110111010001011100101001101110101011000100110101001100101011000110111010001001110011000010110110101100101010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000000110100101100110010000001010000110001101100001011100110100001101101111011011100110011001101001011001110101110010000110110000101110011010000110110000101110100011001010110011101101111011100100111100101000000111101011110101000000100111001100001011010010111010001110011010111001000011011011110110110101101101011011110110111001011100101001101101000011000010111001001100101011001000101110010000110110000101110011010000110110000101110100011001010110011101101111011100100111100101011100101010001101000011100100110010101100101010000110100000101110011010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000000100110101100001011101000110100001011100101001001101111011101010110111001100100010100001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010000110110000101100010101100010000001100010101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001001101011000010111010001101000010111001010010011011110111010101101110011001000101000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001011100100001101100001011001001011000100000011000101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000010011010110000101110100011010000101110010100100110111101110101011011100110010001010000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001000011011000010110011010110001000000110001010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101000000110100101100110010000001010000110001101100001011100110100001101101111011011100110011001101001011001110101110010000110110000101110011010000110110000101110100011001010110011101101111011100100111100101000000111101011110101000000100111001100001011010010111010001110011010111001000011011011110110110101101101011011110110111001011100101001101101000011000010111001001100101011001000101110010000110110000101110011010000110110000101110100011001010110011101101111011100100111100101011100101010001110111011011110100001101000001011100110101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001001101011000010111010001101000010111001010010011011110111010101101110011001000101000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001011100100001101100001011000101011000100000011000101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000010011010110000101110100011010000101110010100100110111101110101011011100110010001010000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001000011011000010110010010110001000000110001010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000000100110101100001011101000110100001011100101001001101111011101010110111001100100010100001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010000110110000101100010101100010000001100010101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000110001101101100011000010111001101110011011110101000100111011101110010011000010111000001000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000000100110101100001011101000110100001011100101001001101111011101010110111001100100010100001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010001010111100001100001011011010111001101011000100000011000101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001100011011011000110000101110011011100110111101010001001110111011100100110000101110000010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001001101011000010111010001101000010111001010010011011110111010101101110011001000101000010100001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010000110110000101100010100000010101101000000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001000011011000010110010010000001010110100000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001011100100001101100001011001101000000101011010000001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010001010111100001100001011011010111001101010010101100010000001100010101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001110100011011110111010001100001011011000101001101100011011011110111001001100101010000001111010100000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001011100100001101100001011000101000000101011010000001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010000110110000101100100100000010101101000000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001000011011000010110011010000001010110100000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001011100100010101111000011000010110110101110011011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110110011000010111001001000000110011101110010011000010110010001100101010101100110000101101100011101010110010101000000111101010000001000111011001010111010001000111011100100110000101100100011001010101000010100001100100011011110111010101100010011011000110010101010010111010001101111011101000110000101101100010100110110001101101111011100100110010101010010111011011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100011011010000110000101110010010000001100111011100100110000101100100011001010100000011110101000000100111010110100100111011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101001011001100100000010100001100111011100100110000101100100011001010101011001100001011011000111010101100101010000001111010111101010000001001110100011001001110101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001110111001001100001011001000110010101000000111101010000001001110100011001001110111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110010011001010110110101100001011100100110101101110011010000001111010100000010001001000110011000010110100101101100010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001100111011100100110000101100100011001010101011001100001011011000111010101100101010000001111010111101010000001001110100010101001110101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001110111001001100001011001000110010101000000111101010000001001110100010101001110111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110010011001010110110101100001011100100110101101110011010000001111010100000010001001000110011000010110100101110010010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001100111011100100110000101100100011001010101011001100001011011000111010101100101010000001111010111101010000001001110100010001001110101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001110111001001100001011001000110010101000000111101010000001001110100010001001110111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110010011001010110110101100001011100100110101101110011010000001111010100000010001001010000011000010111001101110011010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001100111011100100110000101100100011001010101011001100001011011000111010101100101010000001111010111101010000001001110100001101001110101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001110111001001100001011001000110010101000000111101010000001001110100001101001110111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110010011001010110110101100001011100100110101101110011010000001111010100000010001001000111011011110110111101100100010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001100111011100100110000101100100011001010101011001100001011011000111010101100101010000001111010111101010000001001110100001001001110101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001110111001001100001011001000110010101000000111101010000001001110100001001001110111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110010011001010110110101100001011100100110101101110011010000001111010100000010001001010110010111001000111011011110110111101100100010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001100111011100100110000101100100011001010101011001100001011011000111010101100101010000001111010111101010000001001110100000101001110101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001110111001001100001011001000110010101000000111101010000001001110100000101001110111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110010011001010110110101100001011100100110101101110011010000001111010100000010001001000101011110000110001101100101011011000110110001100101011011100111010001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000111001101110100011110010110110001100101011110101000100111011101101000011010010111010001100101010110101110011011100000110000101100011011001010111010010000001101110011011110111011101110010011000010111000001110110100000011101110110100101100100011101000110100001110100100000011011001001010100010011111001000000011001110111001001100001011001000110010101111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000101111010111101000010011001010110110001101111011101110100000011100100110010101110100011100100110100101100101011101100110010101000000110000101101100011011000100000011101000110100001100101010000001110011011000110110111101110010011001010111001101000000110111101100110010000001100011011101010111001001110010011001010110111001110100010000001110011011101010110001001101010011001010110001101110100010000001101111011001100100000011001010110000101100011011010000100000011100110111010001110101011001000110010101101110011101000100000011010010110111001000000110001101110101011100100111001001100101011011100111010001000000110001101101100011000010111001101110011010110101100001011100100110110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110110011000010111001001000000110001101110101011100100111001001100101011011100111010001000011011011000110000101110011011100110100000101110010011011010101001101110100011101010110010001100101011011100111010001110011010100110110001101101111011100100110010101110011010000001111010100000011001110111001001010011011101000111010101100100011001010110111001110100010010010110010001000001011011100110010001001111011100100110010001010010011001010111001101110101011011000111010001011100101001101100101011011000110010101100011011101000100110101100001011011100111100101010000111001001000000111101011111001000000111001001010010111011011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110110011000010111001001000000110111101110010011001000110010101110010011001010110010001000011011101010111001001110010011001010110111001110100010100110111010101100010011010100110010101100011011101000100001101101100011000010111001101110011010000010111001001101101010100110111010001110101011001000110010101101110011101000111001101010011011000110110111101110010011001010111001101000000111101010000001100011011101010111001001110010011001010110111001110100010000110110110001100001011100110111001101000001011100100110110101010011011101000111010101100100011001010110111001110100011100110101001101100011011011110111001001100101011100110101110010101110110100001100101011100100110010101010000111001001000000111101011111001000000111001001011100101001101110101011000100110101001100101011000110111010001001001011001000100000011110101111010100000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001011100101001101110101011000100110101001100101011000110111010001001001011001000100000010011001001100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100100101110010100110111010001110101011001000110010101101110011101000101110010000110110110001100001011100110111001101000001011100100110110101001001011001000100000011110101111010100000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001011100101001101110100011101010110010001100101011011100111010001011100100001101101100011000010111001101110011010000010111001001101101010010010110010001010010101110010011110111001001100100011001010111001001000010011110010100010001100101011100110110001101100101011011100110010001101001011011100110011101010000110111101000000111101011111001000000110111101011100100001101100001011000101000000101011010000001101111010111001000011011000010110010010000001010110100000011011110101110010000110110000101100110100000010101101000000110111101011100100010101111000011000010110110101110011010100101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110111001101111010011110110011001010011011101000111010101100100011001010110111001110100011100110100100101101110010101000110100001100101010000010111001001101101010011110110011001100110011001010111001001101001011011100110011101010100011010000110010101010011011101010110001001101010011001010110001101110100010000001111010100000010100001100010011110010111010001100101010100101101111011100100110010001100101011100100110010101100100010000110111010101110010011100100110010101101110011101000101001101110101011000100110101001100101011000110111010001000011011011000110000101110011011100110100000101110010011011010101001101110100011101010110010001100101011011100111010001110011010100110110001101101111011100100110010101110011010111001000011011011110111010101101110011101000101000010100101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111001101110101011000100110101001100101011000110111010001001100011011110111011101100101011100110111010001010011011000110110111101110010011001010100000011110101000000110111101110010011001000110010101110010011001010110010001000011011101010111001001110010011001010110111001110100010100110111010101100010011010100110010101100011011101000100001101101100011000010111001101110011010000010111001001101101010100110111010001110101011001000110010101101110011101000111001101010011011000110110111101110010011001010111001101011100100110101101001011011100101000011100110100000011110101111100100000011100110101110010000110110000101100010100000010101101000000111001101011100100001101100001011001001000000101011010000001110011010111001000011011000010110011010000001010110100000011100110101110010001010111100001100001011011010111001101010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100110111010101100010011010100110010101100011011101000100100001101001011001110110100001100101011100110111010001010011011000110110111101110010011001010100000011110101000000110111101110010011001000110010101110010011001010110010001000011011101010111001001110010011001010110111001110100010100110111010101100010011010100110010101100011011101000100001101101100011000010111001101110011010000010111001001101101010100110111010001110101011001000110010101101110011101000111001101010011011000110110111101110010011001010111001101011100100110101100001011110000101000011100110100000011110101111100100000011100110101110010000110110000101100010100000010101101000000111001101011100100001101100001011001001000000101011010000001110011010111001000011011000010110011010000001010110100000011100110101110010001010111100001100001011011010111001101010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100110111010101100010011010100110010101100011011101000100000101110110011001010111001001100001011001110110010101010011011000110110111101110010011001010100000011110101000000110111101110010011001000110010101110010011001010110010001000011011101010111001001110010011001010110111001110100010100110111010101100010011010100110010101100011011101000100001101101100011000010111001101110011010000010111001001101101010100110111010001110101011001000110010101101110011101000111001101010011011000110110111101110010011001010111001101011100101001101110101011011010101000011100110100000011110101111100100000011100110101110010000110110000101100010100000010101101000000111001101011100100001101100001011001001000000101011010000001110011010111001000011011000010110011010000001010110100000011100110101110010001010111100001100001011011010111001101010010100000010111101000000110111001101111010011110110011001010011011101000111010101100100011001010110111001110100011100110100100101101110010101000110100001100101010000010111001001101101010011110110011001100110011001010111001001101001011011100110011101010100011010000110010101010011011101010110001001101010011001010110001101110100011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110100011001010110000101100011011010000110010101110010011100110100111001100001011011010110010101000000111101010000001101111011100100110010001100101011100100110010101100100010000110111010101110010011100100110010101101110011101000101001101110101011000100110101001100101011000110111010001000011011011000110000101110011011100110100000101110010011011010101001101110100011101010110010001100101011011100111010001110011010100110110001101101111011100100110010101110011010111001000110011010010111001001110011011101000101000010100101011100100010101101101011100000110110001101111011110010110010101100101010111001010101011100110110010101110010010111001000110011101010110110001101100010011100110000101101101011001010111011011010101001101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011010010110111001100100011010010111011001101001011001000111010101100001011011000100111001100001011011010110010101000000111101010000001110100011001010110000101100011011010000110010101110010011100110100111001100001011011010110010101011100101001101110000011011000110100101110100010100001001110100000010011101010010101110010101000110111101001100011010010111001101110100010100001010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100110111010001110010011010010110111001100111010000001110011011001010110001101101111011011100110010001001110011000010110110101100101010000001111010100000011011100111010101101100011011000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011010010110011001000000101000010000101110011011101000111001001101001011011100110011101011100100100101110011010011100111010101101100011011000100111101110010010101110110100001101001011101000110010101010011011100000110000101100011011001010101000011010010110111001100100011010010111011001101001011001000111010101100001011011000100111001100001011011010110010101011011011000101011101010100101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111001101100101011000110110111101101110011001000100111001100001011011010110010101000000111101010000001101001011011100110010001101001011101100110100101100100011101010110000101101100010011100110000101101101011001010101101101100010101110101011011011000001011101010111001010100011011110101010101110000011100000110010101110010010100001010010100000010101101000000100010010111001000000100010011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110100011001010110000101100011011010000110010101110010011100110100111001100001011011010110010101000000111101010000001101001011011100110010001101001011101100110100101100100011101010110000101101100010011100110000101101101011001010101101101100000101110101011011011000001011101010111001010100011011110101010101110000011100000110010101110010010100001010010100000010101101000000100010010111001000000100010010000001010110100000010100001110011011001010110001101101111011011100110010001001110011000010110110101100101010000001111110111111010000001110011011001010110001101101111011011100110010001001110011000010110110101100101010100101000000101011010000001101001011011100110010001101001011101100110100101100100011101010110000101101100010011100110000101101101011001010101101101100100101110101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011001101111011100100110010101100001011000110110100001000000101000011101100110000101110010010000001100101011000010110001101101000010010010111010001100101011011010100000011010010110111001000000110111101110010011001000110010101110010011001010110010001000011011101010111001001110010011001010110111001110100010100110111010101100010011010100110010101100011011101000100001101101100011000010111001101110011010000010111001001101101010100110111010001110101011001000110010101101110011101000111001101010011011000110110111101110010011001010111001101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110100101100110010000001010000110010101100001011000110110100001001001011101000110010101101101010111001010011011101000111010101100100011001010110111001110100010010010110010001000000111101011110101000000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001010011011101000111010101100100011001010110111001110100010010010110010001010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100010011100100110010101100001011010110111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110011011101010110001001101010011001010110001101110100010100110110001101101111011100100110010101010000011011110111001101101001011101000110100101101111011011100100000011110101000000111001101110101011000100110101001100101011000110111010001010011011000110110111101110010011001010101000001101111011100110110100101110100011010010110111101101110010000001010110100000011000101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100011111001000000011100110111010101100010011010100110010101100011011101000101001101100011011011110111001001100101010100000110111101110011011010010111010001101001011011110110111001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001111100100000001101110011011110100111101100110010100110111010001110101011001000110010101101110011101000111001101001001011011100101010001101000011001010100000101110010011011010100111101100110011001100110010101110010011010010110111001100111010101000110100001100101010100110111010101100010011010100110010101100011011101000111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001111100100000001001101011000010111010001101000010111001010010011011110111010101101110011001000101000011100110111010101100010011010100110010101100011011101000100110001101111011101110110010101110011011101000101001101100011011011110111001001100101010110001000000110001010100101111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100011111001000000010011010110000101110100011010000101110010100100110111101110101011011100110010001010000111001101110101011000100110101001100101011000110111010001001000011010010110011101101000011001010111001101110100010100110110001101101111011100100110010101011000100000011000101010010111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001110010011011110111010101101110011001000101001101110101011000100110101001100101011000110111010001000001011101100110010101110010011000010110011101100101010100110110001101101111011100100110010101000000111101010000001001101011000010111010001101000010111001010010011011110111010101101110011001000101000011100110111010101100010011010100110010101100011011101000100000101110110011001010111001001100001011001110110010101010011011000110110111101110010011001010101100010000001100010101001011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100011111001000000010100001110010011011110111010101101110011001000101001101110101011000100110101001100101011000110111010001000001011101100110010101110010011000010110011101100101010100110110001101101111011100100110010101010010111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000110001101101111011011000111001101110000011000010110111001111010100010011001001000100100000011100110111010001111001011011000110010101111010100010010001001111100100000001110010011001010110110101100001011100100110101101110011011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000000101010011110001110100011001000100000011100110111010001111001011011000110010101111010100010011101110110100001101001011101000110010101011010111001101110000011000010110001101100101011101001000000110111001101111011101110111001001100001011100000111011010000001110111011010010110010001110100011010000111010010000001101100100101010001001111100100000001110100011001010110000101100011011010000110010101110010011100110100111001100001011011010110010101111000101111011101000110010001111100101010010000000110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001110010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110111001101111010011110110011001010011011101010110001001101010011001010110001101110100010011110110011001100110011001010111001001100101011001000100000011110101000000110010101100001011000110110100001010011011101000111010101100100011001010110111001110100010111001000111011100100110111101110101011100000100001001111001010100001111000010000001111010111110010000001111000010111001010011011101010110001001101010011001010110001101110100010010010110010001010010101110010000110110111101110101011011100111010001010000101001011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011001110111001001101111011101010111000001000010011110010101001101110101011000100110101001100101011000110111010001000000111101010000001100101011000010110001101101000010100110111010001110101011001000110010101101110011101000101110010001110111001001101111011101010111000001000010011110010101000011100110100000011110101111100100000011100110101110010100110111010101100010011010100110010101100011011101000100100101100100010100101011100100111101110010011001000110010101110010010000100111100101010000111100001000000111101011111001000000111100001011100100101101100101011110010101001011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011001101111011100100110010101100001011000110110100001000000101000011101100110000101110010010000001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000100000011010010110111001000000110011101110010011011110111010101110000010000100111100101010011011101010110001001101010011001010110001101110100010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001110011011101010110001001101010011001010110001101110100010100110110001101101111011100100110010101010000011011110111001101101001011101000110100101101111011011100100000011110101000000110001011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000100111100101110100011001010100000011011100110111101001111011001100101001101110100011101010110010001100101011011100111010001110011010010010110111001010100011010000110010101000001011100100110110101001111011001100110011001100101011100100110100101101110011001110101010001101000011001010101001101110101011000100110101001100101011000110111010001000000111101010000001100000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011100110111010101100010011010100110010101100011011101000100110001101111011101110110010101110011011101000101001101100011011011110111001001100101010000001111010100000011000001011100110000011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001110011011101010110001001101010011001010110001101110100010010000110100101100111011010000110010101110011011101000101001101100011011011110111001001100101010000001111010100000011000001011100110000011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001110011011101010110001001101010011001010110001101110100010000010111011001100101011100100110000101100111011001010101001101100011011011110111001001100101010000001111010100000011000001011100110000011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000110111010101110010011100100110010101101110011101000101001101110100011101010110010001100101011011100111010001001001011001000100000011110101000000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001000110011010010111001001110011011101000101000010100101011100101001101110100011101010110010001100101011011100111010001001001011001000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011101000110010101100001011000110110100001100101011100100111001101001110011000010110110101100101010000001111010100000010001001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011100100110010101101101011000010111001001101011011100110100000011110101000000100010010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001011110101111010000001110110011000010111001001000000111001101110100011101010110010001100101011011100111010001010011011101010110001001101010011001010110001101110100010100000110111101110011011010010111010001101001011011110110111001000000111101010000001100010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000111001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000110001101101100011000010111001101110011011110101000100111011101110010011000010111000001000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000010010000111010001101101011011000101110010001000110100101110011011100000110110001100001011110010100011001101111011100100101000011011010110111101100100011001010110110001001001011101000110010101101101010000001111010111110010000001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010001100110100101110010011100110111010001010000101001010111001010011011101010110001001101010011001010110001101110100010111001010011011101010110001001101010011001010110001101110100010011100110000101101101011001010101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000011010010110011001000000101000011000110110000101110011010000110110111101101110011001100110100101100111010111001000011011000010111001101000011011000010111010001100101011001110110111101110010011110010100000011110101111010100000010011100110000101101001011101000111001101011100100001101101111011011010110110101101111011011100101110010100110110100001100001011100100110010101100100010111001000011011000010111001101000011011000010111010001100101011001110110111101110010011110010101110010101000110100001110010011001010110010101000011010000010111001101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000010100001001101011000010111010001101000010111001010010011011110111010101101110011001000101000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001011100101001101110101011011010101000011110000100000011110101111100100000011110000101110010000110110000101100010101001010000001011110100000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001011100100001101101111011101010110111001110100010100001010010101100010000001100010101001010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000000101000010011010110000101110100011010000101110010100100110111101110101011011100110010001010000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001010011011101010110110101010000111100001000000111101011111001000000111100001011100100001101100001011001001010010100000010111101000000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001000011011011110111010101101110011101000101000010100101011000100000011000101010010101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001010000100110101100001011101000110100001011100101001001101111011101010110111001100100010100001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010100110111010101101101010100001111000010000001111010111110010000001111000010111001000011011000010110011010100101000000101111010000001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010000110110111101110101011011100111010001010000101001010110001000000110001010100101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010100000011010010110011001000000101000011000110110000101110011010000110110111101101110011001100110100101100111010111001000011011000010111001101000011011000010111010001100101011001110110111101110010011110010100000011110101111010100000010011100110000101101001011101000111001101011100100001101101111011011010110110101101111011011100101110010100110110100001100001011100100110010101100100010111001000011011000010111001101000011011000010111010001100101011001110110111101110010011110010101110010101000111011101101111010000110100000101110011010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000000101000010011010110000101110100011010000101110010100100110111101110101011011100110010001010000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001010011011101010110110101010000111100001000000111101011111001000000111100001011100100001101100001011000101010010100000010111101000000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001000011011011110111010101101110011101000101000010100101011000100000011000101010010101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001010000100110101100001011101000110100001011100101001001101111011101010110111001100100010100001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010100110111010101101101010100001111000010000001111010111110010000001111000010111001000011011000010110010010100101000000101111010000001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010000110110111101110101011011100111010001010000101001010110001000000110001010100101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000110001101101100011000010111001101110011011110101000100111011101110010011000010111000001000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001010000100110101100001011101000110100001011100101001001101111011101010110111001100100010100001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010100110111010101101101010100001111000010000001111010111110010000001111000010111001000011011000010110001010100101000000101111010000001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010000110110111101110101011011100111010001010000101001010110001000000110001010100101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001100011011011000110000101110011011100110111101010001001110111011100100110000101110000010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000000101000010011010110000101110100011010000101110010100100110111101110101011011100110010001010000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001010011011101010110110101010000111100001000000111101011111001000000111100001011100100010101111000011000010110110101110011010100101000000101111010000001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010000110110111101110101011011100111010001010000101001010110001000000110001010100101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011000110110110001100001011100110111001101111010100010011101110111001001100001011100000100010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001010000100110101100001011101000110100001011100101001001101111011101010110111001100100010100001010000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001010011011101010110110101010000111100001000000111101011111001000000111100001011100100001101100001011000101010010100000010111101000000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001000011011011110111010101101110011101000101000010100101010010100000010101101000000101000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001011100101001101110101011011010101000011110000100000011110101111100100000011110000101110010000110110000101100100101001010000001011110100000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001011100100001101101111011101010110111001110100010100001010010101001010000001010110100000010100001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010100110111010101101101010100001111000010000001111010111110010000001111000010111001000011011000010110011010100101000000101111010000001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010000110110111101110101011011100111010001010000101001010100101000000101011010000001010000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001010011011101010110110101010000111100001000000111101011111001000000111100001011100100010101111000011000010110110101110011010100101000000101111010000001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010000110110111101110101011011100111010001010000101001010100101011000100000011000101010010101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001110100011011110111010001100001011011000101001101100011011011110111001001100101010000001111010100000010100001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010100110111010101101101010100001111000010000001111010111110010000001111000010111001000011011000010110001010100101000000101111010000001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010000110110111101110101011011100111010001010000101001010100101000000101011010000001010000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001010011011101010110110101010000111100001000000111101011111001000000111100001011100100001101100001011001001010010100000010111101000000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001000011011011110111010101101110011101000101000010100101010010100000010101101000000101000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001011100101001101110101011011010101000011110000100000011110101111100100000011110000101110010000110110000101100110101001010000001011110100000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001011100100001101101111011101010110111001110100010100001010010101001010000001010110100000010100001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010100110111010101101101010100001111000010000001111010111110010000001111000010111001000101011110000110000101101101011100110101001010000001011110100000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001011100100001101101111011101010110111001110100010100001010010101001011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011001110111001001100001011001000110010101010110011000010110110001110101011001010100000011110101000000100011101100101011101000100011101110010011000010110010001100101010100001110100011011110111010001100001011011000101001101100011011011110111001001100101010100101110110110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100011011010000110000101110010010000001100111011100100110000101100100011001010100000011110101000000100111010110100100111011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110100101100110010000001010000110011101110010011000010110010001100101010101100110000101101100011101010110010101000000111101011110101000000100111010001100100111010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001110111001001100001011001000110010101000000111101010000001001110100011001001110111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111001001100101011011010110000101110010011010110111001101000000111101010000001000100100011001100001011010010110110001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001100111011100100110000101100100011001010101011001100001011011000111010101100101010000001111010111101010000001001110100010101001110101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011101110010011000010110010001100101010000001111010100000010011101000101010011101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110010011001010110110101100001011100100110101101110011010000001111010100000010001001000110011000010110100101110010010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010100000011010010110011001000000101000011001110111001001100001011001000110010101010110011000010110110001110101011001010100000011110101111010100000010011101000100010011101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100111011100100110000101100100011001010100000011110101000000100111010001000100111011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100100110010101101101011000010111001001101011011100110100000011110101000000100010010100000110000101110011011100110100010011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101000000110100101100110010000001010000110011101110010011000010110010001100101010101100110000101101100011101010110010101000000111101011110101000000100111010000110100111010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001110111001001100001011001000110010101000000111101010000001001110100001101001110111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111001001100101011011010110000101110010011010110111001101000000111101010000001000100100011101101111011011110110010001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001100111011100100110000101100100011001010101011001100001011011000111010101100101010000001111010111101010000001001110100001001001110101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011101110010011000010110010001100101010000001111010100000010011101000010010011101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110010011001010110110101100001011100100110101101110011010000001111010100000010001001010110010111001000111011011110110111101100100010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010100000011010010110011001000000101000011001110111001001100001011001000110010101010110011000010110110001110101011001010100000011110101111010100000010011101000001010011101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100111011100100110000101100100011001010100000011110101000000100111010000010100111011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100100110010101101101011000010111001001101011011100110100000011110101000000100010010001010111100001100011011001010110110001101100011001010110111001110100010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001110011011101000111100101101100011001010111101010001001000100111110010000000110011101110010011000010110010001100101011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111101011110100001001100101011011000110111101110111010000001110010011001010111010001110010011010010110010101110110011001010100000011000010110110001101100010000001110100011010000110010101000000111001101100011011011110111001001100101011100110100000011011110110011001000000110001101110101011100100111001001100101011011100111010001000000111001101110101011000100110101001100101011000110111010001000000110111101100110010000001100101011000010110001101101000010000001110011011101000111010101100100011001010110111001110100010000001101001011011100100000011000110111010101110010011100100110010101101110011101000100000011000110110110001100001011100110111001101011010110000101110010011011010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001100011011101010111001001110010011001010110111001110100010000110110110001100001011100110111001101000001011100100110110101010011011101000111010101100100011001010110111001110100011100110101001101100011011011110111001001100101011100110100000011110101000000110011101110010010100110111010001110101011001000110010101101110011101000100100101100100010000010110111001100100010011110111001001100100010100100110010101110011011101010110110001110100010111001010011011001010110110001100101011000110111010001001101011000010110111001111001010100001110010010000001111010111110010000001110010010100101011100101010001101111010011000110100101110011011101000101000010100101110110110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001011110101111010101000110100001100101010000001100011011000010111001101100101010000001101111011001100100000011000100111001001100001011000110110101101100101011101000110100101101110011001110100000011100110111010001110101011001000110010101101110011101000110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001101111011100100110010001100101011100100110010101100100010000110111010101110010011100100110010101101110011101000101001101110101011000100110101001100101011000110111010001000011011011000110000101110011011100110100000101110010011011010101001101110100011101010110010001100101011011100111010001110011010100110110001101101111011100100110010101110011010000001111010100000011000110111010101110010011100100110010101101110011101000100001101101100011000010111001101110011010000010111001001101101010100110111010001110101011001000110010101101110011101000111001101010011011000110110111101110010011001010111001101011100101011101101000011001010111001001100101010100001110010010000001111010111110010000001110010010111001010011011101010110001001101010011001010110001101110100010010010110010001000000111101011110101000000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001000110011010010111001001110011011101000101000010100101011100101001101110101011000100110101001100101011000110111010001001001011001000100000010011001001100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110010010111001010011011101000111010101100100011001010110111001110100010111001000011011011000110000101110011011100110100000101110010011011010100100101100100010000001111010111101010000001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010001100110100101110010011100110111010001010000101001010111001010011011101000111010101100100011001010110111001110100010111001000011011011000110000101110011011100110100000101110010011011010100100101100100010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010111001000111011100100110111101110101011100000100001001111001010100001110110010000001111010111110010000001110110010111001010011011101000111010101100100011001010110111001110100010010010110010001010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001011100100111101110010011001000110010101110010010000100111100101000100011001010111001101100011011001010110111001100100011010010110111001100111010100001101111010000001111010111110010000001010000110111101011100101001101110101011011010101000011000100100000011110101111100100000011000100101110010000110110000101100010101001010000001011110100000011011110101110010000110110111101110101011011100111010001010000101001010000001010110100000011011110101110010100110111010101101101010100001100010010000001111010111110010000001100010010111001000011011000010110010010100101000000101111010000001101111010111001000011011011110111010101101110011101000101000010100101000000101011010000001101111010111001010011011101010110110101010000110001001000000111101011111001000000110001001011100100001101100001011001101010010100000010111101000000110111101011100100001101101111011101010110111001110100010100001010010100000010101101000000110111101011100101001101110101011011010101000011000100100000011110101111100100000011000100101110010001010111100001100001011011010111001101010010100000010111101000000110111101011100100001101101111011101010110111001110100010100001010010101001010100101110110100000010111101011110100000010101101000000110001001011100100001101100001011001001000000101011010000001100010010111001000011011000010110011010000001010110100000011000100101110010001010111100001100001011011010111001101101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011011100110111101001111011001100101001101110100011101010110010001100101011011100111010001110011010010010110111001010100011010000110010101000001011100100110110101001111011001100110011001100101011100100110100101101110011001110101010001101000011001010101001101110101011000100110101001100101011000110111010001000000111101010000001010000110001001111001011101000110010101010010110001101110101011100100111001001100101011011100111010001000011011011000110000101110011011100110100000101110010011011010101001101110100011101010110010001100101011011100111010001110011010100110110001101101111011100100110010101110011010111001010111011010000110010101110010011001010101000011100100100000011110101111100100000011100100101110010100110111010101100010011010100110010101100011011101000100100101100100010000001111010111101010000001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010001100110100101110010011100110111010001010000101001010111001010011011101010110001001101010011001010110001101110100010010010110010001000000100110010011001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100100101110010100110111010001110101011001000110010101101110011101000101110010000110110110001100001011100110111001101000001011100100110110101001001011001000100000011110101111010100000011001010110000101100011011010000101001101110101011000100110101001100101011000110111010001011100100011001101001011100100111001101110100010100001010010101110010100110111010001110101011001000110010101101110011101000101110010000110110110001100001011100110111001101000001011100100110110101001001011001000101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000101110010001110111001001101111011101010111000001000010011110010101000011101100100000011110101111100100000011101100101110010110010110010101100001011100100110110001111001010000010110111001100100010101000110010101110010011011010110110001111001010100110111010001100001011101000111010101110011010010010110010001010010101110010000010111011001100101011100100110000101100111011001010101000011000100100000011110101111100100000011000100101110010000110110111101110101011011100111010001010000101001010100101110110110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110011011101010110001001101010011001010110001101110100010011000110111101110111011001010111001101110100010100110110001101101111011100100110010101000000111101010000001101111011100100110010001100101011100100110010101100100010000110111010101110010011100100110010101101110011101000101001101110101011000100110101001100101011000110111010001000011011011000110000101110011011100110100000101110010011011010101001101110100011101010110010001100101011011100111010001110011010100110110001101101111011100100110010101110011010111001001101011010010110111001010000110111101000000111101011111001000000101000011011110101110010000010111011001100101011100100110000101100111011001010101000011000100100000011110101111100100000011000100101110010000110110000101100010101001010000001010110100000011011110101110010100110111010101101101010100001100010010000001111010111110010000001100010010111001000011011000010110010010100101000000101111010000001101111010111001000011011011110111010101101110011101000101000010100101000000101011010000001101111010111001010011011101010110110101010000110001001000000111101011111001000000110001001011100100001101100001011001101010010100000010111101000000110111101011100100001101101111011101010110111001110100010100001010010100000010101101000000110111101011100101001101110101011011010101000011000100100000011110101111100100000011000100101110010001010111100001100001011011010111001101010010100000010111101000000110111101011100100001101101111011101010110111001110100010100001010010101001010100101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100110111010101100010011010100110010101100011011101000100100001101001011001110110100001100101011100110111010001010011011000110110111101110010011001010100000011110101000000110111101110010011001000110010101110010011001010110010001000011011101010111001001110010011001010110111001110100010100110111010101100010011010100110010101100011011101000100001101101100011000010111001101110011010000010111001001101101010100110111010001110101011001000110010101101110011101000111001101010011011000110110111101110010011001010111001101011100100110101100001011110000101000011011110100000011110101111100100000010100001101111010111001010011011101010110110101010000110001001000000111101011111001000000110001001011100100001101100001011000101010010100000010111101000000110111101011100100001101101111011101010110111001110100010100001010010100000010101101000000110111101011100101001101110101011011010101000011000100100000011110101111100100000011000100101110010000110110000101100100101001010000001011110100000011011110101110010000110110111101110101011011100111010001010000101001010000001010110100000011011110101110010100110111010101101101010100001100010010000001111010111110010000001100010010111001000011011000010110011010100101000000101111010000001101111010111001000011011011110111010101101110011101000101000010100101000000101011010000001101111010111001010011011101010110110101010000110001001000000111101011111001000000110001001011100100010101111000011000010110110101110011010100101000000101111010000001101111010111001000011011011110111010101101110011101000101000010100101010010101001011101101101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100110111010101100010011010100110010101100011011101000100000101110110011001010111001001100001011001110110010101010011011000110110111101110010011001010100000011110101000000110111101110010011001000110010101110010011001010110010001000011011101010111001001110010011001010110111001110100010100110111010101100010011010100110010101100011011101000100001101101100011000010111001101110011010000010111001001101101010100110111010001110101011001000110010101101110011101000111001101010011011000110110111101110010011001010111001101011100101001101110101011011010101000011011110100000011110101111100100000010100001101111010111001010011011101010110110101010000110001001000000111101011111001000000110001001011100100001101100001011000101000000101011010000001100010010111001000011011000010110010010000001010110100000011000100101110010000110110000101100110100000010101101000000110001001011100100010101111000011000010110110101110011010100101000000101111010000001101111010111001000011011011110111010101101110011101000101000010100101010010101001010000001011110100000011011100110111101001111011001100101001101110100011101010110010001100101011011100111010001110011010010010110111001010100011010000110010101000001011100100110110101001111011001100110011001100101011100100110100101101110011001110101010001101000011001010101001101110101011000100110101001100101011000110111010001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101000110010101100001011000110110100001100101011100100111001101001110011000010110110101100101010000001111010100000011000110111010101110010011100100110010101101110011101000100001101101100011000010111001101110011010000010111001001101101010100110111010001110101011001000110010101101110011101000111001101010011011000110110111101110010011001010111001101011100101011101101000011001010111001001100101010100001110010010000001111010111110010000001110010010111001010011011101010110001001101010011001010110001101110100010010010110010001000000111101011110101000000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001000110011010010111001001110011011101000101000010100101011100101001101110101011000100110101001100101011000110111010001001001011001000100000010011001001100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111001001011100101001101110100011101010110010001100101011011100111010001011100100001101101100011000010111001101110011010000010111001001101101010010010110010001000000111101011110101000000110010101100001011000110110100001010011011101010110001001101010011001010110001101110100010111001000110011010010111001001110011011101000101000010100101011100101001101110100011101010110010001100101011011100111010001011100100001101101100011000010111001101110011010000010111001001101101010010010110010001010010101110010011110111001001100100011001010111001001000010011110010101000011101100100000011110101111100100000011101100101110010110010110010101100001011100100110110001111001010000010110111001100100010101000110010101110010011011010110110001111001010100110111010001100001011101000111010101110011010010010110010001010010101110010011000110000101110011011101000101000010100101011100100010101101101011100000110110001101111011110010110010101100101010111001010101011100110110010101110010010111001000110011101010110110001101100010011100110000101101101011001010111011011010101001101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001101001011011100110010001101001011101100110100101100100011101010110000101101100010011100110000101101101011001010100000011110101000000111010001100101011000010110001101101000011001010111001001110011010011100110000101101101011001010101110010100110111000001101100011010010111010001010000100111010000001001110101001010111001010100011011110100110001101001011100110111010001010000101001011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111001101110100011100100110100101101110011001110100000011100110110010101100011011011110110111001100100010011100110000101101101011001010100000011110101000000110111001110101011011000110110001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011010010110011001000000101000010000101110011011101000111001001101001011011100110011101011100100100101110011010011100111010101101100011011000100111101110010010101110110100001101001011101000110010101010011011100000110000101100011011001010101000011010010110111001100100011010010111011001101001011001000111010101100001011011000100111001100001011011010110010101011011011000101011101010100101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110011011001010110001101101111011011100110010001001110011000010110110101100101010000001111010100000011010010110111001100100011010010111011001101001011001000111010101100001011011000100111001100001011011010110010101011011011000101011101010110110110000010111010101110010101000110111101010101011100000111000001100101011100100101000010100101000000101011010000001000100101110010000001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111010001100101011000010110001101101000011001010111001001110011010011100110000101101101011001010100000011110101000000110100101101110011001000110100101110110011010010110010001110101011000010110110001001110011000010110110101100101010110110110000010111010101101101100000101110101011100101010001101111010101010111000001110000011001010111001001010000101001010000001010110100000010001001011100100000010001001000000101011010000001010000111001101100101011000110110111101101110011001000100111001100001011011010110010101000000111111011111101000000111001101100101011000110110111101101110011001000100111001100001011011010110010101010010100000010101101000000110100101101110011001000110100101110110011010010110010001110101011000010110110001001110011000010110110101100101010110110110010010111010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100110011011110111001001100101011000010110001101101000010000001010000111011001100001011100100100000011001010110000101100011011010000100100101110100011001010110110101000000110100101101110010000001101111011100100110010001100101011100100110010101100100010000110111010101110010011100100110010101101110011101000101001101110101011000100110101001100101011000110111010001000011011011000110000101110011011100110100000101110010011011010101001101110100011101010110010001100101011011100111010001110011010100110110001101101111011100100110010101110011010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011010010110011001000000101000011001010110000101100011011010000100100101110100011001010110110101011100100011001101001011100100111001101110100010100001010010101110010100110111010001110101011001000110010101101110011101000100100101100100010000001111010111101010000001100101011000010110001101101000010100110111010101100010011010100110010101100011011101000101110010001100110100101110010011100110111010001010000101001010111001010011011101000111010101100100011001010110111001110100010010010110010001010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000100111001001100101011000010110101101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111001101110101011000100110101001100101011000110111010001010011011000110110111101110010011001010101000001101111011100110110100101110100011010010110111101101110010000001111010100000011100110111010101100010011010100110010101100011011101000101001101100011011011110111001001100101010100000110111101110011011010010111010001101001011011110110111001000000101011010000001100010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100011111001000000011100110111010101100010011010100110010101100011011101000101001101100011011011110111001001100101010100000110111101110011011010010111010001101001011011110110111001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100011111001000000011011100110111101001111011001100101001101110100011101010110010001100101011011100111010001110011010010010110111001010100011010000110010101000001011100100110110101001111011001100110011001100101011100100110100101101110011001110101010001101000011001010101001101110101011000100110101001100101011000110111010001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001111100100000001001101011000010111010001101000010111001010010011011110111010101101110011001000101000011100110111010101100010011010100110010101100011011101000100110001101111011101110110010101110011011101000101001101100011011011110111001001100101010110001000000110001010100101111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001111100100000001001101011000010111010001101000010111001010010011011110111010101101110011001000101000011100110111010101100010011010100110010101100011011101000100100001101001011001110110100001100101011100110111010001010011011000110110111101110010011001010101100010000001100010101001011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001110010011011110111010101101110011001000101001101110101011000100110101001100101011000110111010001000001011101100110010101110010011000010110011101100101010100110110001101101111011100100110010101000000111101010000001001101011000010111010001101000010111001010010011011110111010101101110011001000101000011100110111010101100010011010100110010101100011011101000100000101110110011001010111001001100001011001110110010101010011011000110110111101110010011001010101100010000001100010101001011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000111110010000000101000011100100110111101110101011011100110010001010011011101010110001001101010011001010110001101110100010000010111011001100101011100100110000101100111011001010101001101100011011011110111001001100101010100101111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000110001101101111011011000111001101110000011000010110111001111010100010011001001000100111110010000000111001001100101011011010110000101110010011010110111001101111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000111001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011000100110111101100100011110010111110011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001100001011000100110110001100101011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011001000111110011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000111001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110000101100010011011000110010101111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000110010001101001011101100100000011100110111010001111001011011000110010101111010100010011001100110110001101111011000010111010001110100100000011011000110010101100110011101000100010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000111010100100100000101000100010001010100000010001000100010101010100010000010100100101001100010100110111010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101100100011010010111011001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000110010001101001011101100100000011100110111010001111001011011000110010101111010100010011001100110110001101111011000010111010001110100100000011100100110100101100111011010000111010001000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110011011101000111001001101111011011100110011101111100100111001110101011011010110001001100101011100100100000011011110110011001000000101001101110101011000100110101001100101011000110111010001000000100111101100110011001100110010101110010011001010110010001110100100000011110001011110111001101110100011100100110111101101110011001110111110010000001111000111001101110000011000010110111001000000111001101110100011110010110110001100101011110101000100111000001100001011001000110010001101001011011100110011101011010111001001101001011001110110100001110100011101001000000110010011001010110110101110110100000011000110110111101101100011011110111001001110100100000011100100110010101100100010001001111100100000001101110011011110100111101100110010100110111010101100010011010100110010101100011011101000100111101100110011001100110010101110010011001010110010001111000101111011100110111000001100001011011100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101100100011010010111011001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000110010001101001011101100100000011100110111010001111001011011000110010101111010100010011000110110110001100101011000010111001001110100100000011000100110111101110100011010000100010011111001111000101111011001000110100101110110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001100100011010010111011001111100111000011000001011010110001011000001100000100000011110101000000100000101010000100010101111000011000110110010101101100011011000110010101101110011000110110010101010010100000011011101100000101101011011101110010100000011110101000000100001001010000101011001011100100011101101111011011110110010001010010100000011011001100000101101011011001110010100000011110101000000100001101010000100011101101111011011110110010001010010100000011010101100000101101011010101110010100000011110101000000100010001010000101000001100001011100110111001101010010100000011010001100000101101011010001110010100000011110101000000100010101010000100011001100001011010010111001001010010100000011000001011010110011011100101000000111101010000001000110010100001000110011000010110100101101100010100101111000101111011001000110100101110110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001100100011010010111011001000000111001101110100011110010110110001100101011110101000100110001001101111011100100110010001100101011100100101101011100110111010001111001011011000110010101110100100000011100110110111101101100011010010110010001110110100000011011010110000101110010011001110110100101101110010110101100010011011110111010001110100011011110110110101110100100000011001001110000011110000100010011111001111000101111011001000110100101110110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101001011001100100000010100001100001011001100110011001100101011000110111010001101001011101100110010101000001011011100110010001010000011100110111100101100011011010000110111101101101011011110111010001101111011100100100000101110011011100110110010101110011011100110110110101100101011011100111010001001100011010010111001101110100010000001000010111101010000001101110011101010110110001101100010000001001100100110010000001100001011001100110011001100101011000110111010001101001011101100110010101000001011011100110010001010000011100110111100101100011011010000110111101101101011011110111010001101111011100100100000101110011011100110110010101110011011100110110110101100101011011100111010001001100011010010111001101110100010111001000011011011110111010101101110011101000100000011111001000000110000010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101001011001100100000010100001101111011100100110010001100101011100100100000101100110011001100110010101100011011101000110100101110110011001010100000101101110011001000101000001110011011110010110001101101000011011110110110101101111011101000110111101110010010000010111001101110011011001010111001101110011011011010110010101101110011101000100110001101001011100110111010001011100100001101101111011101010110111001110100010000001111100100000011000001010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101111011100100110010001100101011100100100000101100110011001100110010101100011011101000110100101110110011001010100000101101110011001000101000001110011011110010110001101101000011011110110110101101111011101000110111101110010010000010111001101110011011001010111001101110011011011010110010101101110011101000100110001101001011100110111010001011100100001101101100011001010110000101110010010100001010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101111011100100110010001100101011100100100000101100110011001100110010101100011011101000110100101110110011001010100000101101110011001000101000001110011011110010110001101101000011011110110110101101111011101000110111101110010010000010111001101110011011001010111001101110011011011010110010101101110011101000100110001101001011100110111010001011100100000101100100011001000101001001100001011011100110011101100101010100001100001011001100110011001100101011000110111010001101001011101100110010101000001011011100110010001010000011100110111100101100011011010000110111101101101011011110111010001101111011100100100000101110011011100110110010101110011011100110110110101100101011011100111010001001100011010010111001101110100010111001010111011010000110010101110010011001010101000011000010100000011110101111100100000011000010101110010100110111010001110101011001000110010101101110011101000101110010010010110010001000000111101011110101000000110001101110101011100100111001001100101011011100111010001010011011101000111010101100100011001010110111001110100010010010110010001000000100110010011001000000110000101011100101100101100101011000010111001001101100011110010100000101101110011001000101010001100101011100100110110101101100011110010100000101110011011100110110010101110011011100110110110101100101011011100111010001010011011101000110000101110100011101010111001101011100101010001100101011100100110110101101100011110010100000101100011011000110110010101110011011100110110110101100101011011100111010001010011011101000110000101110100011101010111001101000000111101011110101000000101010001100101011100100110110101101100011110010100000101100011011000110110010101110011011100110110110101100101011011100111010001010011011101000110000101110100011101010111001101011100100001101110101011100100111001001100101011011100111010001010010101110010011110111001001100100011001010111001001000010011110010101000011000010100000011110101111100100000011000010101110010010010111010001100101011011010100100101100100010100101010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110110011000010111001001000000110011101100101011101000100000101100110011001100110010101100011011101000110100101110110011001010100000101101110011001000101000001110011011110010110001101101000011011110110110101101111011101000110111101110010010000001111010100000011011100110010101110111010000001001100011010010111001101110100011110001000001011001100110011001100101011000110111010001101001011101100110010101000001011011100110010001010000011100110111100101100011011010000110111101101101011011110111010001101111011100100100000101110011011100110110010101110011011100110110110101100101011011100111010001001111011101010111010001110000011101010111010001011000011101000111001001100001011000110111010001101001011011110110111001000110011011110111001001000110011011110111001001101101010101000110010101100001011000110110100001100101011100100100010001110100011011110111110010100001010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100001011000100110110001100101010000001100011011011000110000101110011011100110111101010001001110100011000010110001001101100011001010100000011101000110000101100010011011000110010101011010110001001101111011100100110010001100101011100100110010101100100010001001000000111001101110100011110010110110001100101011110101000100111011101101001011001000111010001101000011101001000000110001011000001100000100101010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100010011011110110010001111001011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001110010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100001011000100110110001100101010000001100011011011000110000101110011011100110111101010001001110100011000010110001001101100011001010100000011101000110000101100010011011000110010101011010110001001101111011100100110010001100101011100100110010101100100010000001110100011000010110001001101100011001010100011001101111011011100111010001010011011010010111101001100101010001001000000111001101110100011110010110110001100101011110101000100111011101101001011001000111010001101000011101001000000110001011000001100000100101010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110110011000010111001001000000110001101101111011101010110111001110100011001010111001001000000111101010000001100000111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101001011001100100000010100001100111011001010111010001000001011001100110011001100101011000110111010001101001011101100110010101000001011011100110010001010000011100110111100101100011011010000110111101101101011011110111010001101111011100100101110010000010110111001111001010100001010010101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110011101100101011101000100000101100110011001100110010101100011011101000110100101110110011001010100000101101110011001000101000001110011011110010110001101101000011011110110110101101111011101000110111101110010010111001000011011011000110010101100001011100100101000010100101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001110110010101110100010000010110011001100110011001010110001101110100011010010111011001100101010000010110111001100100010100000111001101111001011000110110100001101111011011010110111101110100011011110111001001011100100000101100100011001000101001001100001011011100110011101100101010100001101111011100100110010001100101011100100100000101100110011001100110010101100011011101000110100101110110011001010100000101101110011001000101000001110011011110010110001101101000011011110110110101101111011101000110111101110010010000010111001101110011011001010111001101110011011011010110010101101110011101000100110001101001011100110111010001011100101010001100001011010110110010101010000110110010100101010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001110010011111001111000111010001100100010000001100011011011110110110001110011011100000110000101101110011110101000100110010010001001000000110001101101100011000010111001101110011011110101000100111010001100101011110000111010001011010110001101100101011011100111010001100101011100100100010011111001000000100000101000110010001100100010101000011010101000100100101010110010001010100000010000010101001101010011010001010101001101010011010011010100010101001110010101000111100010111101110100011001000111110011110001011110111010001110010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000111001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001101000010000001110011011101000111100101101100011001010111101010001001110100011001010111100001110100010110101100001011011000110100101100111011011100111010010000001101100011001010110011001110100010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100100101110100011001010110110101110011010011101000000100111001100001011011010110010101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001101000011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011010000100000011100110111010001111001011011000110010101111010100010011101000110010101111000011101000101101011000010110110001101001011001110110111001110100100000011000110110010101101110011101000110010101110010010001001111100101001101100011011011110111001001100101011100110111100010111101110100011010000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000111001001111100110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100110011011110111001001100101011000010110001101101000010000001010000111011001100001011100100100000011010010111010001100101011011010100000011010010110111001000000110011101100101011101000100000101100110011001100110010101100011011101000110100101110110011001010100000101101110011001000101000001110011011110010110001101101000011011110110110101101111011101000110111101110010010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000110110111101110101011011100111010001100101011100100100000011110101000000110001101101111011101010110111001110100011001010111001001000000101011010000001100010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000111001001000000111001101110100011110010110110001100101011110101000100111000001100001011001000110010001101001011011100110011101011010111010001101111011100000111010010000001100000111000001111000011101101000000111000001100001011001000110010001101001011011100110011101011010110001001101111011101000111010001101111011011010111010010000001100000111000001111000010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000111001101110100011110010110110001100101011110101000100111000001100001011001000110010001101001011011100110011101011010111010001101111011100000111010010000001100000111000001111000011101101000000111000001100001011001000110010001101001011011100110011101011010110001001101111011101000111010001101111011011010111010010000001100000111000001111000010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000000100100001110100011011010110110001011100100010001101001011100110111000001101100011000010111100101000110011011110111001001010000110110101101111011001000110010101101100010010010111010001100101011011010100000011110101111100100000011010010111010001100101011011010101110010000010110011001100110011001010110001101110100011010010111011001100101010000010110111001100100010100000111001101111001011000110110100001101111011011010110111101110100011011110111001001011100100100101110100011001010110110101110011010011100110000101101101011001010101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001110011011101000111100101101100011001010111101010001001110000011000010110010001100100011010010110111001100111010110101110100011011110111000001110100100000011000001110000011110000111011010000001110000011000010110010001100100011010010110111001100111010110101100010011011110111010001110100011011110110110101110100100000011000001110000011110000111011010000001110100011001010111100001110100010110101100001011011000110100101100111011011100111010010000001100011011001010110111001110100011001010111001001000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100100011110010110111001100001011011010110100101100011010000001110011011000110110111101110010011001010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110100101100110010000001010000110100101110100011001010110110101011100101001101100011011011110111001001100101010000001111010111101010000001101100101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110011011000110110111101110010011001010100000011110101000000100010010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101000000110100101100110010000001010000110100101110100011001010110110101011100101001101100011011011110111001001100101010000001111010111101010000001100000101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110011011000110110111101110010011001010100000011110101000000100010010110101000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100110110001101101111011100100110010101000000111101010000001101001011101000110010101101101010111001010011011000110110111101110010011001010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001110011011000110110111101110010011001010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011001000111110011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001110010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011000010110001001101100011001010111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110000101100010011011000110010101000000110001101101100011000010111001101110011011110101000100111010001100001011000100110110001100101010000001110100011000010110001001101100011001010101101011000100110111101110010011001000110010101110010011001010110010001000000111010001100001011000100110110001100101010001100110111101101110011101000101001101101001011110100110010101000100100000011100110111010001111001011011000110010101111010100010011101110110100101100100011101000110100001110100100000011000101100000110000010010101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110100101100110010000001010000110011101100101011101000100000101100110011001100110010101100011011101000110100101110110011001010100000101101110011001000101000001110011011110010110001101101000011011110110110101101111011101000110111101110010010111001000001011011100111100101010000101001010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001110110010101110100010000010110011001100110011001010110001101110100011010010111011001100101010000010110111001100100010100000111001101111001011000110110100001101111011011010110111101110100011011110111001001011100100001101101100011001010110000101110010010100001010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100011011011110111010101101110011101000110010101110010010000001111010100000011000001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001110110010101110100010000010110011001100110011001010110001101110100011010010111011001100101010000010110111001100100010100000111001101111001011000110110100001101111011011010110111101110100011011110111001001011100100000101100100011001000101001001100001011011100110011101100101010100001101111011100100110010001100101011100100100000101100110011001100110010101100011011101000110100101110110011001010100000101101110011001000101000001110011011110010110001101101000011011110110110101101111011101000110111101110010010000010111001101110011011001010111001101110011011011010110010101101110011101000100110001101001011100110111010001011100101001101101011011010010111000001010000110110010100101010010111011011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000111001001111100111100011101000110010001000000110001101101111011011000111001101110000011000010110111001111010100010011001001000100100000011000110110110001100001011100110111001101111010100010011101000110010101111000011101000101101011000110110010101101110011101000110010101110010010001001111100100000010100000101001101011001010000110100100001001111010011010100111101010100010011110101001001000000100000101010011010100110100010101010011010100110100110101000101010011100101010001111000101111011101000110010001111100111100010111101110100011100100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001110010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011010000100000011100110111010001111001011011000110010101111010100010011101000110010101111000011101000101101011000010110110001101001011001110110111001110100100000011011000110010101100110011101000100010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001001001011101000110010101101101011100110100111010000001001110011000010110110101100101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011010000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110100001111100101001101100011011011110111001001100101011100110111100010111101110100011010000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000111001001111100110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100110011011110111001001100101011000010110001101101000010000001010000111011001100001011100100100000011010010111010001100101011011010100000011010010110111001000000110011101100101011101000100000101100110011001100110010101100011011101000110100101110110011001010100000101101110011001000101000001110011011110010110001101101000011011110110110101101111011101000110111101110010010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011000110110111101110101011011100111010001100101011100100100000011110101000000110001101101111011101010110111001110100011001010111001001000000101011010000001100010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000111001001000000111001101110100011110010110110001100101011110101000100111000001100001011001000110010001101001011011100110011101011010111010001101111011100000111010010000001100000111000001111000011101101000000111000001100001011001000110010001101001011011100110011101011010110001001101111011101000111010001101111011011010111010010000001100000111000001111000010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000111001101110100011110010110110001100101011110101000100111000001100001011001000110010001101001011011100110011101011010111010001101111011100000111010010000001100000111000001111000011101101000000111000001100001011001000110010001101001011011100110011101011010110001001101111011101000111010001101111011011010111010010000001100000111000001111000010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000000100100001110100011011010110110001011100100010001101001011100110111000001101100011000010111100101000110011011110111001001010000110110101101111011001000110010101101100010010010111010001100101011011010100000011110101111100100000011010010111010001100101011011010101110010000010110011001100110011001010110001101110100011010010111011001100101010000010110111001100100010100000111001101111001011000110110100001101111011011010110111101110100011011110111001001011100100100101110100011001010110110101110011010011100110000101101101011001010101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001110011011101000111100101101100011001010111101010001001110000011000010110010001100100011010010110111001100111010110101110100011011110111000001110100100000011000001110000011110000111011010000001110000011000010110010001100100011010010110111001100111010110101100010011011110111010001110100011011110110110101110100100000011000001110000011110000111011010000001110100011001010111100001110100010110101100001011011000110100101100111011011100111010010000001100011011001010110111001110100011001010111001001000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100100011110010110111001100001011011010110100101100011010000001110011011000110110111101110010011001010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110100101100110010000001010000110100101110100011001010110110101011100101001101100011011011110111001001100101010000001111010111101010000001101100101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110011011000110110111101110010011001010100000011110101000000100010010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101000000110100101100110010000001010000110100101110100011001010110110101011100101001101100011011011110111001001100101010000001111010111101010000001100000101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110011011000110110111101110010011001010100000011110101000000100010010110101000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011100110110001101101111011100100110010101000000111101010000001101001011101000110010101101101010111001010011011000110110111101110010011001010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001110011011000110110111101110010011001010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011100100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011000010110001001101100011001010111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011100100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011000100110111101100100011110010111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110000101100010011011000110010101111100110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000110100001101010100000011100110111010001111001011011000110010101111010100010011011010110000101110010011001110110100101101110010110101101100011001010110011001110100011101001000000110000101110101011101000110111101110110100000011011010110000101110010011001110110100101101110010110101110010011010010110011101101000011101000111010010000001100001011101010111010001101111011101101000000111011101101001011001000111010001101000011101001000000110101011010101100000111000001111000010001001111100110101010000001111010100000010000010101000010001010111100001100011011001010110110001101100011001010110111001100011011001010101001010000001101000100000011110101000000100001001010000101011001011100100011101101111011011110110010001010010100000011001101000000111101010000001000011010100001000111011011110110111101100100010100101000000110010010000001111010100000010001000101000010100000110000101110011011100110101001010000001100010100000011110101000000100010101010000100011001100001011010010111001001010010100000011000001000000111101010000001000110010100001000110011000010110100101101100010100101111000101111011010000110101011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011010000110110010000001110011011101000111100101101100011001010111101010001001100010011011110111001001100100011001010111001001011010111001101110100011110010110110001100101011101001000000110010001101111011101000111010001100101011001000100010011111001111000101111011010000110110011111001101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001100100011010010111011001000000110001101101100011000010111001101110011011110101000100110001101101111011011000101101011011010110010001011010110001011001001000100100000011100110111010001111001011011000110010101111010100010010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000110011011110111001001101101010000001010100011001010110000101100011011010000110010101110010010011101110011010000001000011011011110110110101101101011001010110111001110100011101001000000111100011100110111000001100001011011100100000011100110111010001111001011011000110010101111010100010011001100110111101101110011101000101101011100110111010001111001011011000110010101110100100000011010010111010001100001011011000110100101100011010001001000000110001101101100011000010111001101110011011110101000100111010001100101011110000111010001011010111000001110010011010010110110101100001011100100111100101000100111110010000000101000010000101110011011101000111001001101001011011100110011101011100100100101110011010011100111010101101100011011000100111101110010010101110110100001101001011101000110010101010011011100000110000101100011011001010101000011101000110010101100001011000110110100001100101011100100100001101101111011011010110110101100101011011100111010001110011010100101000000111111010000001110100011001010110000101100011011010000110010101110010010000110110111101101101011011010110010101101110011101000111001101000000111010010000001000100100111001101111010000001000011011011110110110101101101011001010110111001110100010001001010010111100010111101110011011100000110000101101110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101100100011010010111011001111100110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000110010001101001011101100100000011000110110110001100001011100110111001101111010100010011000110110111101101100010110101101101011001000101101011000101100100100010010000001110011011101000111100101101100011001010111101010001001110000011000010110010001100100011010010110111001100111010110101110100011011110111000001110100100000011000001011100110101011001010110110101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110000101100010011011000110010101000000110001101101100011000010111001101110011011110101000100111010001100001011000100110110001100101010000001110100011000010110001001101100011001010100011001101111011011100111010001010011011010010111101001100101010001001000000111001101110100011110010110110001100101011110101000100111011101101001011001000111010001101000011101001000000110001011000001100000100101010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000111001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011000110110111101101100011100110111000001100001011011100111101010001001100100100010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010001100110111101110010011011010100000010101000110010101100001011000110110100001100101011100100100111011100110100000010011100110000101101101011001010111010011110001110011011100000110000101101110010000001110011011101000111100101101100011001010111101010001001100110011011110110111001110100010110101110011011101000111100101101100011001010111010010000001101001011101000110000101101100011010010110001101110110100000011100000110000101100100011001000110100101101110011001110101101011011000110010101100110011101000111010010000001100010110010101101101010001001000000110001101101100011000010111001101110011011110101000100111010001100101011110000111010001011010111000001110010011010010110110101100001011100100111100101000100111110010000000110011001101111011100100110110101001101011000010111001101110100011001010111001001110011010011100110000101101101011001010111100010111101110011011100000110000101101110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001100011011011000110000101110011011100110111101010001001110100011001010111100001110100010110101110010011010010110011101101000011101000100010011111001010011011010010110011101101110011000010111010001110101011100100110010101110100100000011110001011110111010001100100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110100101100110010000001010000111001101110100011100100110100101101110011001110101110010010010111001101001110011101010110110001101100010011110111001001010111011010000110100101110100011001010101001101110000011000010110001101100101010100001100110011011110111001001101101010011010110000101110011011101000110010101110010011100110101001101101001011001110110111001100001011101000111010101110010011001010101001010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100110011011110111001001101101010011010110000101110011011101000110010101110010011100110101001101101001011001110110111001100001011101000111010101110010011001010100000011110101000000100010010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001000010101000010100110111100101110011011101000110010101101101010111001001001010011110101110010001100110100101101100011001010101110010001010111100001101001011100110111010001110011010100001010000011000010111010001101000010111001000011011011110110110101100010011010010110111001100101010100001010011011001010111001001110110011001010111001001011100100110101100001011100000101000001100001011101000110100001010000100010011111100101111010000110110111101101110011101000110010101101110011101000101111010100110110100101100111011011100110000101110100011101010111001001100101011100110101111010001001010010101100010000001100110011011110111001001101101010011010110000101110011011101000110010101110010011100110101001101101001011001110110111001100001011101000111010101110010011001010101001010100101010010101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001100110111101110010011011010100110101100001011100110111010001100101011100100111001101010011011010010110011101101110011000010111010001110101011100100110010101000000111101010000001000100100010011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101001011001100100000010100001000010111001101110100011100100110100101101110011001110101110010010010111001101001110011101010110110001101100010011110111001001010111011010000110100101110100011001010101001101110000011000010110001101100101010100001100110011011110111001001101101010011010110000101110011011101000110010101110010011100110101001101101001011001110110111001100001011101000111010101110010011001010101001010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000110100101101101011001110100000011000110110110001100001011100110111001101111010100010011010010110110101100111010110101110010011001010111001101110000011011110110111001110011011010010111011001100101010001001000000111001101110100011110010110110001100101011110101000100111011101101001011001000111010001101000011101001000000110011011001010110110101110110100000011010000110010101101001011001110110100001110100011101001000000110001010111001101010110010101101101010001001000000111001101110010011000110111101010001001111110010111101000011011011110110111001110100011001010110111001110100010111101010011011010010110011101101110011000010111010001110101011100100110010101110011010111101000000011001100110111101110010011011010100110101100001011100110111010001100101011100100111001101010011011010010110011101101110011000010111010001110101011100100110010101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111001101110000011000010110111001111100101111101000000101111101000000101111101000000101111101000000101111101000000101111101000000101111101000000101111101000000101111101000000101111101000000101111101000000101111101000000101111101111000101111011100110111000001100001011011100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011100100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011100100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001100100010000001100011011011110110110001110011011100000110000101101110011110101000100110100010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110100101100110010000001010000111001101100011011010000110111101101111011011000100100001100101011000010110010001000000100001011110101000000110111001110101011011000110110001000000100110010011001000000111001101100011011010000110111101101111011011000100100001100101011000010110010001011100101001101100011011010000110111101101111011011000100100001100101011000010110010001000011011000010111010001100101011001110110111101110010011110010100100101100100010000001111010111101010000001100010101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001101001011001100100000010100001110011011000110110100001101111011011110110110001001000011001010110000101100100010111001000111011001010110111001100100011001010111001001000000111101011110101000000100011101100101011011100110010001100101011100100101110010001100110010101101101011000010110110001100101010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011010000110010101100001011001000101010001101001011101000110110001100101010000001111010100000010001001001000011001010110000101100100011011010110100101110011011101000111001001100101011100110111001101000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001111011010000001101000011001010110000101100100010101000110100101110100011011000110010101000000111101010000001000100100100001100101011000010110010001101101011000010111001101110100011001010111001001000100111011010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000110010001101001011101100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001101000011001010110000101100100010101000110100101110100011011000110010101001110111001101000000100001101101111011011010110110101100101011011100111010001110100100000011110001110011011100000110000101101110010000001110011011101000111100101101100011001010111101010001001100110011011110110111001110100010110101110011011101000111100101101100011001010111010010000001101001011101000110000101101100011010010110001101110110100000011100000110000101100100011001000110100101101110011001110101101011011000110010101100110011101000111010010000001100010110010101101101010001001000000110001101101100011000010111001101110011011110101000100111010001100101011110000111010001011010111000001110010011010010110110101100001011100100111100101000100111110010000000101000010000101110011011101000111001001101001011011100110011101011100100100101110011010011100111010101101100011011000100111101110010010101110110100001101001011101000110010101010011011100000110000101100011011001010101000011100110110001101101000011011110110111101101100010010000110010101100001011001000100001101101111011011010110110101100101011011100111010001110011010100101000000111111010000001110011011000110110100001101111011011110110110001001000011001010110000101100100010000110110111101101101011011010110010101101110011101000111001101000000111010010000001000100100111001101111010000001000011011011110110110101101101011001010110111001110100010001001010010111100010111101110011011100000110000101101110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101100100011010010111011001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001110011011000110110100001101111011011110110110001001000011001010110000101100100010000001000010111101010000001101110011101010110110001101100010000001001100100110010000001110011011000110110100001101111011011110110110001001000011001010110000101100100010111001010011011000110110100001101111011011110110110001001000011001010110000101100100010000110110000101110100011001010110011101101111011100100111100101001001011001000100000011110101111010100000011001001010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011001000110100101110110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010100000111001001101001011011100110001101101001011100000110000101101100010011101110011010000001000011011011110110110101101101011001010110111001110100011101001000000111100011100110111000001100001011011100100000011100110111010001111001011011000110010101111010100010011001100110111101101110011101000101101011100110111010001111001011011000110010101110100100000011010010111010001100001011011000110100101100011011101101000000111000001100001011001000110010001101001011011100110011101011010110110001100101011001100111010001110100100000011000101100101011011010100010010000001100011011011000110000101110011011100110111101010001001110100011001010111100001110100010110101110000011100100110100101101101011000010111001001111001010001001111100100000001010000100001011100110111010001110010011010010110111001100111010111001001001011100110100111001110101011011000110110001001111011100100101011101101000011010010111010001100101010100110111000001100001011000110110010101010000111001101100011011010000110111101101111011011000100100001100101011000010110010001000011011011110110110101101101011001010110111001110100011100110101001010000001111110100000011100110110001101101000011011110110111101101100010010000110010101100001011001000100001101101111011011010110110101100101011011100111010001110011010000001110100100000010001001001110011011110100000010000001000011011011110110110101101101011001010110111001110100010001001010010111100010111101110011011100000110000101101110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101100100011010010111011001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001100100011010010111011001111100110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011001000110100101110110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001110010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001110010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000110001101101111011011000111001101110000011000010110111001111010100010011001001000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011010010110011001000000101000011100110110001101101000011011110110111101101100010010000110010101100001011001000100000010000101111010100000011011100111010101101100011011000100000010011001001100100000011100110110001101101000011011110110111101101100010010000110010101100001011001000101110010100110110001101101000011011110110111101101100010010000110010101100001011001000100001101100001011101000110010101100111011011110111001001111001010010010110010001000000111101011110101000000110001010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000110010001101001011101100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001101000011001010110000101100100010101000110100101110100011011000110010101001110111001101000000100111001100001011011010110010101110100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111001101110000011000010110111001000000111001101110100011110010110110001100101011110101000100110011001101111011011100111010001011010111001101110100011110010110110001100101011101001000000110100101110100011000010110110001101001011000110111011010000001110000011000010110010001100100011010010110111001100111010110101101100011001010110011001110100011101001000000110001011001010110110101000100100000011000110110110001100001011100110111001101111010100010011101000110010101111000011101000101101011100000111001001101001011011010110000101110010011110010100010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000011100110110001101101000011011110110111101101100010010000110010101100001011001000101110010010000110010101100001011001000111001101001110011000010110110101100101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110011011100000110000101101110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101100100011010010111011001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001110011011000110110100001101111011011110110110001001000011001010110000101100100010000001000010111101010000001101110011101010110110001101100010000001001100100110010000001110011011000110110100001101111011011110110110001001000011001010110000101100100010111001010011011000110110100001101111011011110110110001001000011001010110000101100100010000110110000101110100011001010110011101101111011100100111100101001001011001000100000011110101111010100000011001001010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011001000110100101110110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010100000111001001101001011011100110001101101001011100000110000101101100010011101110011010000001001110011000010110110101100101011101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110011011100000110000101101110010000001110011011101000111100101101100011001010111101010001001100110011011110110111001110100010110101110011011101000111100101101100011001010111010010000001101001011101000110000101101100011010010110001101110110100000011100000110000101100100011001000110100101101110011001110101101011011000110010101100110011101000111010010000001100010110010101101101010001001000000110001101101100011000010111001101110011011110101000100111010001100101011110000111010001011010111000001110010011010010110110101100001011100100111100101000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000000111001101100011011010000110111101101111011011000100100001100101011000010110010001011100100100001100101011000010110010001110011010011100110000101101101011001010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011100110111000001100001011011100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011001000110100101110110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011001000110100101110110011111001101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110110010001101001011101100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000100000011000110110110001100001011100110111001101111010100010011101000110010101111000011101000101101011100100110100101100111011010000111010001000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000101001101101001011001110110111001100001011101000111010101110010011001010111010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000101111011101000110010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001110011011000110110100001101111011011110110110001101100010010000110010101100001011001000101001101101001011001110110111001100001011101000111010101110010011001010100000011110101000000100010010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011100110110100101100111011011100110000101110100011101010111001001100101010000001111010100000011100110110001101101000011011110110111101101100010010000110010101100001011001000100000011110101111010100000011011100111010101101100011011000100000011111101000000100010010001001000000111010010000001110011011000110110100001101111011011110110110001001000011001010110000101100100010111001010011011010010110011101101110011000010111010001110101011100100110010101001001011011010110000101100111011001010101001101110100011100100110100101101110011001110100000011110101111010100000011011100111010101101100011011000100000011111101000000100010010001001000000111010010000001110011011000110110100001101111011011110110110001001000011001010110000101100100010111001010011011010010110011101101110011000010111010001110101011100100110010101001001011011010110000101100111011001010101001101110100011100100110100101101110011001110101110010101000111001001101001011011010101000010100101110110110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011010010110011001000000101000011100110110100101100111011011100110000101110100011101010111001001100101010000001111010111101010000001000100100010010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110011011000110110100001101111011011110110110001101100010010000110010101100001011001000101001101101001011001110110111001100001011101000111010101110010011001010100000011110101000000100010010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101010000001101001011001100100000010100001000010101000010100110111100101110011011101000110010101101101010111001001001010011110101110010001100110100101101100011001010101110010001010111100001101001011100110111010001110011010100001010000011000010111010001101000010111001000011011011110110110101100010011010010110111001100101010100001010011011001010111001001110110011001010111001001011100100110101100001011100000101000001100001011101000110100001010000100010011111100101111010000110110111101101110011101000110010101101110011101000101111010100110110100101100111011011100110000101110100011101010111001001100101011100110101111010001001010010101100010000001110011011010010110011101101110011000010111010001110101011100100110010101010010101001010100101010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111001101100011011010000110111101101111011011000110110001001000011001010110000101100100010100110110100101100111011011100110000101110100011101010111001001100101010000001111010100000010001001000100111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110011011000110110100001101111011011110110110001101100010010000110010101100001011001000101001101101001011001110110111001100001011101000111010101110010011001010100000011110101000000111001101101001011001110110111001100001011101000111010101110010011001010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011010010110011001000000101000011100110110001101101000011011110110111101101100011011000100100001100101011000010110010001010011011010010110011101101110011000010111010001110101011100100110010101000000100001011110101000000100010010001001010010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011010010110110101100111010000001100011011011000110000101110011011100110111101010001001101001011011010110011101011010111001001100101011100110111000001101111011011100111001101101001011101100110010101000100100000011100110111010001111001011011000110010101111010100010011101110110100101100100011101000110100001110100100000011001101100101011011010111011010000001101000011001010110100101100111011010000111010001110100100000011000101011100110101011001010110110101000100100000011100110111001001100011011110101000100111111001011110100001101101111011011100111010001100101011011100111010001011110101001101101001011001110110111001100001011101000111010101110010011001010111001101011110100000001110011011000110110100001101111011011110110110001101100010010000110010101100001011001000101001101101001011001110110111001100001011101000111010101110010011001010100010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001010110110001110011011001010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011100110111000001100001011011100111110010111110100000010111110100000010111110100000010111110100000010111110100000010111110100000010111110100000010111110100000010111110100000010111110100000010111110100000010111110100000010111110111100010111101110011011100000110000101101110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001110010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111000111010001110010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011101000110010001000000110001101101100011000010111001101110011011110101000100111010001100101011110000111010001011010111001001101001011001110110100001110100010001001000000110001101101111011011000111001101110000011000010110111001111010100010011010001000100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100111001100101011110000111010001000000101010001100101011100100110110101000000100001001100101011001110110100101101110011100110111010010000001111000111001101110000011000010110111001000000111001101110100011110010110110001100101011110101000100110011001101111011011100111010001011010111001101110100011110010110110001100101011101001000000110100101110100011000010110110001101001011000110100010010000001100011011011000110000101110011011100110111101010001001110100011001010111100001110100010110101110000011100100110100101101101011000010111001001111001010001001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001110110011000010111001001000000110111001100101011110000111010001010100011001010111001001101101010000100110010101100111011010010110111001110011010001000110000101110100011001010100000011110101000000100010010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011101100110000101110010010000001100001011100110111001101100101011100110111001101101101011001010110111001110100010000001111010100000011000010110011001100110011001010110001101110100011010010111011001100101010000010110111001100100010100000111001101111001011000110110100001101111011011010110111101110100011011110111001001000001011100110111001101100101011100110111001101101101011001010110111001110100010011000110100101110011011101000101110010001100110100101110010011100110111010001001111011100100100010001100101011001100110000101110101011011000111010001010000101001011101101101010100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011010010110011001000000101000011000010111001101110011011001010111001101110011011011010110010101101110011101000100000010000101111010100000011011100111010101101100011011000101001011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111011001100001011100100100000011101000110010101110010011011010100010001100001011101000110010101000000111101010000001100001011001100110011001100101011000110111010001101001011101100110010101000001011011100110010001010000011100110111100101100011011010000110111101101101011011110111010001101111011100100100000101110011011100110110010101110011011100110110110101100101011011100111010001001100011010010111001101110100010111001000110011010010111001001110011011101000101000010100101011100101100101100101011000010111001001101100011110010100000101101110011001000101010001100101011100100110110101101100011110010100000101110011011100110110010101110011011100110110110101100101011011100111010001010011011101000110000101110100011101010111001101011100100111001100101011110000111010001010100011001010111001001101101010000100110010101100111011010010110111001110011010001000110000101110100011001010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110100101100110010000001010000111010001100101011100100110110101000100011000010111010001100101010000001000010111101010000001101110011101010110110001101100010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110111001100101011110000111010001010100011001010111001001101101010000100110010101100111011010010110111001110011010001000110000101110100011001010100000011110101000000111010001100101011100100110110101000100011000010111010001100101010111001010110011000010110110001110101011001010101110010101000110111101001100011011110110111001100111010001000110000101110100011001010101001101110100011100100110100101101110011001110101000010100101110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110111001100101011110000111010001010100011001010111001001101101010000100110010101100111011010010110111001110011010001000110000101110100011001010100000011110101000000100010010011100110111101000000111001101100101011101000100000011001000110000101110100011001010100000011110010110010101110100010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000110111001100101011110000111010001010100011001010111001001101101010000100110010101100111011010010110111001110011010001000110000101110100011001010100000011110101000000100010010011100110111101000000111001101100101011101000100000011001000110000101110100011001010100000011110010110010101110100010001001110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000011011100110010101111000011101000101010001100101011100100110110101000010011001010110011101101001011011100111001101000100011000010111010001100101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110011011100000110000101101110011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001110010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001100001011000100110110001100101011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101100100011010010111011001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011001000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001110010011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111010001100010011011110110010001111001011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110100011000010110001001101100011001010111110011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011100110111010001111001011011000110010101111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100010011011110110010001111001010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011001100110111101101110011101000101101011100110110100101111010011001010111010010000001100010110100011100000111100001000000100001011010010110110101110000011011110111001001110100011000010110111001110100011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111001101110100011110010110110001100101011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000011010010110011001000000101000010011010110111101100100011001010110110001000000100001011110101000000110111001110101011011000110110001000000100110010011001000000100110101101111011001000110010101101100010111001000001011011100111100101010000101001010100101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110011011000110111001001101001011100000111010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100100010100001100110011101010110111001100011011101000110100101101111011011100100000010100001010010100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010010001010000100111010001101110000011100100110100101101110011101000110001001110101011101000111010001101111011011100100111010100101011100111001001100101011011010110111101110110011001010100001101101100011000010111001101110011010100001001110110111001101111010110101100100011010010111001101110000011011000110000101111001010011101010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010101001011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111001101100011011100100110100101110000011101000111110011010101001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000110010101101100011100110110010101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000111101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110000010000001100011011011000110000101110011011100110111101010001001101100011001010110000101100100010000001110100011001010111100001110100010110101100100011000010110111001100111011001010111001001000000111010001100101011110000111010001011010110001101100101011011100111010001100101011100100100010011111001010010011001010111001101110101011011000111010001000000110100101110011010000001101110011011110111010001000000111100101100101011101000100000011100100110010101100001011001000111100101011100111100010111101110000011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001110011011000110111001001101001011100000111010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100100010100001100110011101010110111001100011011101000110100101101111011011100100000010100001010010100000011110110110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010010001010000100111010001101110000011100100110100101101110011101000110001001110101011101000111010001101111011011100100111010100101011100110000101100100011001000100001101101100011000010111001101110011010100001001110110111001101111010110101100100011010010111001101110000011011000110000101111001010011101010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010101001011101101101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011110001011110111001101100011011100100110100101110000011101000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000011110001011110110010001101001011101100111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001100101011011000111001101100101011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011100000100000011000110110110001100001011100110111001101111010100010011011000110010101100001011001000100000011101000110010101111000011101000101101011001000110000101101110011001110110010101110010010000001110100011001010111100001110100010110101100011011001010110111001110100011001010111001001000100111110010100100110010101110011011101010110110001110100010000001101001011100110100000011011100110111101110100010000001111001011001010111010001000000111001001100101011000010110010001111001010111001111000101111011100000111110011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100011100110110001101110010011010010111000001110100011111001101010100100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010010001010000110011001110101011011100110001101110100011010010110111101101110010000001010000101001010000001111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010010001010000100111010001101110000011100100110100101101110011101000110001001110101011101000111010001101111011011100100111010100101011100110000101100100011001000100001101101100011000010111001101110011010100001001110110111001101111010110101100100011010010111001101110000011011000110000101111001010011101010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111110101010010111011011010101001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000111100010111101110011011000110111001001101001011100000111010001111100110101010010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000010000001000000100000011111010110101010011111010";


            if (bytesInString == binaryString)
            {
                var hasEntered = 0;
            }
            else
            {
                var hasSkiped = 0;
            }
            var pageLength = text.Length;

            ArmName arm = armInEnum;
            CurrentClass clas = classNameInEnum;
            ViewBag.classLabel = classNameInEnum + " " + arm;
            var teachersAndOthersService = ServiceLocator.Get<ITeachersAndOthersService>();
            var studentsReports = teachersAndOthersService.ProcessClassArmResultByClassNameAndArmName(sessionId, termInEnum, clas, arm);

            if (studentsReports == null)
            {
                ViewBag.MSG = "Sorry! no student in the selected Arm.";
                return PartialView(new List<StudentsAccessmentDto>());
            }

            if (studentsReports.StatusCode == 1)
            {
                ViewBag.MSG = "Failed! Environment has not been prepared for result processing yet; contact Registration Officer.";
                return PartialView(new List<StudentsAccessmentDto>());
            }
            if (studentsReports?.StudentsAccessment != null && studentsReports.StudentsAccessment.Any())
            {
                var model = teachersAndOthersService.GetSchoolModelForSchoolRegistrationOfficer();

                ViewBag.emailAddress = model.EmailAddress;
                ViewBag.PoAddress = model.PoAddress;
                ViewBag.SchoolAddress = model.SchoolAddress;
                ViewBag.SchoolName = model.SchoolName;
                ViewBag.Telephone = model.Telephone;
                ViewBag.Website = model.Website;
                ViewBag.schoolLogoString = model.SchoolLogoString;
                ViewBag.motto = model.Motto;
                ViewBag.studentsAttendance = studentsReports.StudentsAttendance;
                ViewBag.caSetting = studentsReports.CasSetting;
                if (studentsReports.AffectiveAndPsychomotorAssessment != null && studentsReports.AffectiveAndPsychomotorAssessment.Any())
                {

                    ViewBag.schoolHead = studentsReports.SchoolHeadInfo;
                    ViewBag.armFormMaster = studentsReports.AffectiveAndPsychomotorAssessment.First().FormMaster;
                    ViewBag.affectiveAndPsychomotorAssessment = studentsReports.AffectiveAndPsychomotorAssessment;
                    ViewBag.totalAttendance = studentsReports.StudentsAccessment.OrderBy(x=>x.YearlyAndTermlyStatusId).Last().YearlyAndTermlyStatus.TotalAttendance;
                }
            }
            return PartialView(studentsReports.StudentsAccessment);
        }

        public ActionResult PerClassAndThirdTermCumulativeResultProcessing(short sessionId, TermStatus termInEnum, CurrentClass classNameInEnum)
        {
            var teachersAndOthersService = ServiceLocator.Get<ITeachersAndOthersService>();
            var studentsReports = teachersAndOthersService.ProcessClassResultByClassName(sessionId, termInEnum, classNameInEnum);

            if (studentsReports == null)
            {
                ViewBag.MSG = "Sorry! no student in the selected Arm.";
                return PartialView("PerArmAndThirdTermCumulativeResultProcessing", new List<StudentsAccessmentDto>());
            }

            if (studentsReports.StatusCode == 1)
            {
                ViewBag.MSG = "Failed! Environment has not been prepared for result processing yet; contact Registration Officer.";
                return PartialView("PerArmAndThirdTermCumulativeResultProcessing", new List<StudentsAccessmentDto>());
            }
            if (studentsReports?.StudentsAccessment != null && studentsReports.StudentsAccessment.Any())
            {
                var model = teachersAndOthersService.GetSchoolModelForSchoolRegistrationOfficer();

                ViewBag.emailAddress = model.EmailAddress;
                ViewBag.PoAddress = model.PoAddress;
                ViewBag.SchoolAddress = model.SchoolAddress;
                ViewBag.SchoolName = model.SchoolName;
                ViewBag.Telephone = model.Telephone;
                ViewBag.Website = model.Website;
                ViewBag.schoolLogoString = model.SchoolLogoString;
                ViewBag.motto = model.Motto;
                ViewBag.studentsAttendance = studentsReports.StudentsAttendance;
                ViewBag.caSetting = studentsReports.CasSetting;
                if (studentsReports.AffectiveAndPsychomotorAssessment != null && studentsReports.AffectiveAndPsychomotorAssessment.Any())
                {

                    ViewBag.schoolHead = studentsReports.SchoolHeadInfo;
                    ViewBag.armFormMaster = studentsReports.AffectiveAndPsychomotorAssessment.First().FormMaster;
                    ViewBag.affectiveAndPsychomotorAssessment = studentsReports.AffectiveAndPsychomotorAssessment;
                    ViewBag.totalAttendance = studentsReports.StudentsAccessment.OrderBy(x => x.YearlyAndTermlyStatusId).Last().YearlyAndTermlyStatus.TotalAttendance;
                }
            }
            return PartialView("PerArmAndThirdTermCumulativeResultProcessing", studentsReports.StudentsAccessment);
        }

        public ActionResult GeneralResultProcessing()
        {
            var teachersAndOthersService = ServiceLocator.Get<ITeachersAndOthersService>();
            var studentsReports = teachersAndOthersService.ProcessAllResults();
            if (studentsReports == null)
            {
                ViewBag.MSG = "Sorry! no student in the selected Arm.";
                return PartialView("PerArmAndThirdTermCumulativeResultProcessing", new List<StudentsAccessmentDto>());
            }
            if (studentsReports.StatusCode == 1)
            {
                ViewBag.MSG = "Failed! Environment has not been prepared for result processing yet; contact Registration Officer.";
                return PartialView("PerArmAndThirdTermCumulativeResultProcessing", new List<StudentsAccessmentDto>());
            }
            if (studentsReports?.StudentsAccessment != null && studentsReports.StudentsAccessment.Any())
            {
                var model = teachersAndOthersService.GetSchoolModelForSchoolRegistrationOfficer();

                ViewBag.emailAddress = model.EmailAddress;
                ViewBag.PoAddress = model.PoAddress;
                ViewBag.SchoolAddress = model.SchoolAddress;
                ViewBag.SchoolName = model.SchoolName;
                ViewBag.Telephone = model.Telephone;
                ViewBag.Website = model.Website;
                ViewBag.schoolLogoString = model.SchoolLogoString;
                ViewBag.motto = model.Motto;
                ViewBag.studentsAttendance = studentsReports.StudentsAttendance;
                ViewBag.caSetting = studentsReports.CasSetting;
                if (studentsReports.AffectiveAndPsychomotorAssessment != null && studentsReports.AffectiveAndPsychomotorAssessment.Any())
                {

                    ViewBag.schoolHead = studentsReports.SchoolHeadInfo;
                    ViewBag.armFormMaster = studentsReports.AffectiveAndPsychomotorAssessment.First().FormMaster;
                    ViewBag.affectiveAndPsychomotorAssessment = studentsReports.AffectiveAndPsychomotorAssessment;
                    ViewBag.totalAttendance = studentsReports.AffectiveAndPsychomotorAssessment.First().YearlyAndTermlyAssessmentStatus.TotalAttendance;
                }
            }
            return PartialView(studentsReports.StudentsAccessment);
        }

        public ActionResult SmsIndex()
        {
            return View();
        }
        //static readonly HttpClient Client = new HttpClient();

        private async Task<MessageSentFeedbackDto> ConnectToInfobip(SmsInfoDto smsInfo)
        {
            var inputTelNo = smsInfo.RecipientNo;
            var feedbackTelNos = new List<TelNumberDto>();
            var returnResult = new MessageSentFeedbackDto();

            var basicAuthConfiguration = new BasicAuthConfiguration("https://api.infobip.com/", "naits", "!@2)01Ba8Hs");
            SendSingleTextualSms smsClient = new SendSingleTextualSms(basicAuthConfiguration);
            SMSTextualRequest request = new SMSTextualRequest
            {
                From = smsInfo.SenderName,
                To = smsInfo.RecipientNo,
                Text = smsInfo.Message
            };
            var infobipApi = await smsClient.ExecuteAsync(request);
            var responseDetails = infobipApi.Messages;
            foreach (var eachResponse in responseDetails)
            {
                feedbackTelNos.Add(new TelNumberDto
                {
                    TelNo = eachResponse.To,
                    TelNoStatus = TelNoStatus.OrdinaryNo
                });
            }
            returnResult.TelNos = feedbackTelNos;
            return returnResult;
        }


        private async Task<MessageSentFeedbackDto> ConnectToEbulk(SmsInfoDto smsInfo)
        {
            var inputTelNo = smsInfo.RecipientNo;
            var feedbackTelNos = new List<TelNumberDto>();
            var returnResult = new MessageSentFeedbackDto();
            foreach (var eachNo in inputTelNo)
            {
                var apiresponse = await SmsLib.GetSendSmsAsync1(
                                 "http://api.ebulksms.com:8080/sendsms?username=postmaster@naits-sms.com.ng&apikey=a000f42ae67006fb89bd4ff4cb0dde0191235ed5&sender=" + smsInfo.SenderName + "&messagetext=" +
                                 smsInfo.Message + "&flash=0&recipients=" + eachNo.Trim() + "&dndsender=" + 0);

                if (apiresponse.Contains("SUCCESS"))
                {
                    feedbackTelNos.Add(new TelNumberDto { TelNo = eachNo, TelNoStatus = TelNoStatus.OrdinaryNo });
                }
                else if (apiresponse.Contains("INVALID"))
                {
                    feedbackTelNos.Add(new TelNumberDto { TelNo = eachNo, TelNoStatus = TelNoStatus.InvalidNo });
                }
                else if (apiresponse.Contains("INSUFFICIENT"))
                {
                    returnResult.ExecutionStatus = "Contact NAITS-ASMS Admin to activate more SMS credit on Ebulk.";
                    break;
                }
                else
                {
                    returnResult.ExecutionStatus = "Either Internet problem or other Unknown error.";
                    break;//either Internet problem or other Unknown error and therefore termination is required.
                }
            }
            returnResult.TelNos = feedbackTelNos;
            return returnResult;
        }
        private async Task<MessageSentFeedbackDto> ConnectToJusibe(SmsInfoDto smsInfo)
        {
            var inputTelNo = smsInfo.RecipientNo;
            var feedbackTelNos = new List<TelNumberDto>();
            var returnResult = new MessageSentFeedbackDto();
            foreach (var eachNo in inputTelNo)
            {
                var requestModel = new RequestModel
                {
                    To = eachNo.Trim(),
                    From = smsInfo.SenderName,
                    Message = smsInfo.Message
                };
                var jusibeClient = new JusibeClient("7794de570dcdd94fee7f5cdf6c955cfe",
                    "93a8398d82619645dbec5cdac453f3d1");

                var resoonseResult = await jusibeClient.Send(requestModel);
                if (!string.IsNullOrEmpty(resoonseResult.MessageId))
                {
                    var deliveryStatus = await jusibeClient.GetDeliveryStatus(resoonseResult.MessageId);
                    if (deliveryStatus.Status == "Rejected")
                    {
                        feedbackTelNos.Add(new TelNumberDto
                        {
                            TelNo = eachNo,
                            TelNoStatus = TelNoStatus.DndLine
                        });
                    }
                    else
                    {
                        feedbackTelNos.Add(new TelNumberDto
                        {
                            TelNo = eachNo,
                            TelNoStatus = TelNoStatus.OrdinaryNo
                        });
                    }
                }
                else
                {
                    if (resoonseResult.Status == "Invalid number")
                    {
                        feedbackTelNos.Add(new TelNumberDto
                        {
                            TelNo = eachNo,
                            TelNoStatus = TelNoStatus.InvalidNo
                        });
                    }
                    else if (resoonseResult.Status == "Insufficient credit")
                    {
                        returnResult.ExecutionStatus = "Contact NAITS-ASMS Admin to activate more SMS credit.";
                        break;
                    }
                    else if (resoonseResult.Status.StartsWith("SMS"))
                    {
                        returnResult.ExecutionStatus = resoonseResult.Status;
                        break; //either Internet problem or other Unknown error and therefore termination is required.
                    }
                    else if (resoonseResult.Status == "Internet problem")
                    {
                        returnResult.ExecutionStatus = "Either Internet problem or other Unknown error.";
                        break; //either Internet problem or other Unknown error and therefore termination is required.
                    }
                    else
                    {
                        returnResult.ExecutionStatus = "Unknown error.";
                        break;
                    }
                }
            }
            returnResult.TelNos = feedbackTelNos;
            return returnResult;
        }

        private JusibeClient GetJusibeClient()
        {
            return new JusibeClient("7794de570dcdd94fee7f5cdf6c955cfe",
                    "93a8398d82619645dbec5cdac453f3d1");
        }

        private async Task<string> ConnectToJusibeExpress(SmsInfoDto smsInfo, JusibeClient jusibeClient)
        {
            var inputTelNo = smsInfo.RecipientNo;
            var noOfRecipient = smsInfo.RecipientNo.Count;
            var counter = 0;
            var executionStatus = "";

            foreach (var eachNo in inputTelNo)
            {
                counter = counter + 1;
                var requestModel = new RequestModel
                {
                    To = eachNo.Trim(),
                    From = smsInfo.SenderName,
                    Message = smsInfo.Message
                };
                var responseResult = await jusibeClient.SendSmsExpress(requestModel);//Invalid number
                if (responseResult.Status == "Sent" || responseResult.Status == "Invalid number")
                {
                }
                else if (responseResult.Status.StartsWith("SMS"))
                {
                    executionStatus = responseResult.Status;
                    break; //either Internet problem or other Unknown error and therefore termination is required.
                }
                else if (responseResult.Status == "Internet problem")
                {
                    executionStatus = "Either Internet problem or other Unknown error.";
                    break; //either Internet problem or other Unknown error and therefore termination is required.
                }
                else if (responseResult.Status == "Unknown error")
                {
                    executionStatus = "An Unknown error Occured.";
                    break;
                }
            }
            if (noOfRecipient == counter)
            {
                executionStatus = "Good! messages were sent successfully.";
            }
            return executionStatus;
        }

        public async Task<ActionResult> SendMultipleSms(SmsInfoDto smsInfo)
        {
            ViewBag.Message = "Your contact page.";
            var returnResult = new MessageSentFeedbackDto();
            if (!string.IsNullOrWhiteSpace(smsInfo.Message) && !string.IsNullOrWhiteSpace(smsInfo.SenderName) && smsInfo.RecipientNo != null && smsInfo.RecipientNo.Any())
            {
                var msg = smsInfo.Message;
                //var inputTelNo = smsInfo.RecipientNo;
                //var telNosAndNetworkStatus = new List<TelNoDto>();
                var feedbackTelNos = new List<TelNumberDto>();
                var batchExecutionStatus = "Process has been completed successfully.";
                var checkConnectivity = CheckForInternetConnection();
                returnResult.ExecutionStatus = batchExecutionStatus;
                if (checkConnectivity)
                {
                    var makeTelNoUnique = smsInfo.RecipientNo.Distinct().ToList();
                    var noOfRecipients = makeTelNoUnique.Count;
                    var noOfSmsPages = (float)smsInfo.NoOfSms;
                    var jusibeClient = GetJusibeClient();
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    //For Jusibe SMS Platform TLS Problem fix
                    var sMsBalanceStatusString = await GetSmsBalaceStatus(jusibeClient, noOfRecipients, noOfSmsPages);
                    if (sMsBalanceStatusString.Contains("Success"))
                    {
                        var naitsPlatformSmsBalance = float.Parse(sMsBalanceStatusString.Split(':').ToList()[1]);
                        var pricePerSms = float.Parse(sMsBalanceStatusString.Split(':').ToList()[2]);
                        //telNosAndNetworkStatus.AddRange(from each in makeTelNoUnique let networkType = teachersAndOthersService.GetNetworkType(each) select new TelNoDto { TelNo = each, NetworkType = networkType });
                        //var mtnNos = telNosAndNetworkStatus.Where(s => s.NetworkType == NetworkType.Mtn || s.NetworkType == NetworkType.Others || s.NetworkType == NetworkType.Etisalat);//http://Jusibe.com
                        //var mtnNosWithoutStatus = mtnNos.Select(c => c.TelNo).ToList();

                        //var gloNos = telNosAndNetworkStatus.Where(s => s.NetworkType == NetworkType.Glo || s.NetworkType == NetworkType.Airtel);//http://api.ebulksms.com
                        //var gloNoWithoutStatus = gloNos.Select(c => "234" + c.TelNo.Remove(0, 1)).ToList();
                        //var gloNoWithoutStatusToString = string.Join(",", gloNoWithoutStatus);

                        //var airtelAndEtisalatNos = telNosAndNetworkStatus.Where(s => s.NetworkType == NetworkType.Etisalat || s.NetworkType == NetworkType.Airtel);//infobip.com
                        //var airtelAndEtisalatNosWithoutStatus = airtelAndEtisalatNos.Select(c => c.TelNo).ToList();

                        var jusibeMsg = new SmsInfoDto
                        {
                            Message = msg,
                            SenderName = smsInfo.SenderName,
                            RecipientNo = makeTelNoUnique
                        };
                        //var ebulkMsg = new SmsInfoDto
                        //{
                        //    Message = msg,
                        //    SenderName = smsInfo.SenderName,
                        //    RecipientNo = gloNoWithoutStatus
                        //};

                        //var infobipMsg = new SmsInfoDto
                        //{
                        //    Message = msg,
                        //    SenderName = smsInfo.SenderName,
                        //    RecipientNo = airtelAndEtisalatNosWithoutStatus
                        //};

                        MessageSentFeedbackDto jusibeReturnResult = null;
                        jusibeReturnResult = await ConnectToJusibe(jusibeMsg);

                        //new Thread(
                        //    async delegate () { jusibeReturnResult = await ConnectToJusibe(jusibeMsg); }).Start();

                        //MessageSentFeedbackDto ebulkReturnResult = null;
                        //new Thread(
                        //    async () => { ebulkReturnResult = await ConnectToEbulk(ebulkMsg); }).Start();

                        //MessageSentFeedbackDto infobipReturnResult = null;
                        //new Thread(
                        //    async () => { infobipReturnResult = await ConnectToJusibe(infobipMsg); }).Start();


                        //while (jusibeReturnResult == null || ebulkReturnResult == null || infobipReturnResult == null)//Waiting for all the Threads to complete.
                        //while (jusibeReturnResult == null)
                        //{
                        //    //do nothing
                        //}
                        var noOfSuccessfullySentSms =
                            (float)jusibeReturnResult.TelNos.Count(s => s.TelNoStatus == TelNoStatus.OrdinaryNo);
                        //pricePerSms * noOfSms * noOfTelNo) < balance
                        if (noOfSuccessfullySentSms >= 1f)
                        {
                            var newBalace = naitsPlatformSmsBalance - (pricePerSms * noOfSmsPages * noOfSuccessfullySentSms);
                            var credentialAndSmsBalanceUpdateParameterEncoded =
                                ServiceLocator.Get<ITeachersAndOthersService>()
                                    .GetAndEncodeCredentialAndSmsBalanceUpdateParameters(newBalace,
                                        noOfSuccessfullySentSms,
                                        noOfSmsPages);
                            credentialAndSmsBalanceUpdateParameterEncoded =
                                HttpUtility.UrlEncode(credentialAndSmsBalanceUpdateParameterEncoded);

                            var rightHostName = TheRightNaitsHostName();
                            if (!string.IsNullOrEmpty(rightHostName))
                            {
                                var url2 = rightHostName + "/home/UpdateSmsBalance?encodedParameter=" +
                                       credentialAndSmsBalanceUpdateParameterEncoded;
                                await SmsLib.GetSchoolSmsBalance(url2);//This is actually updating based on the ActionResult "UpdateSmsBalance" called
                            }
                            else
                            {
                                returnResult.ExecutionStatus = "Failed! NAITS-ASMS online platform is temporarily unavailanle!!!";
                            }
                        }

                        feedbackTelNos.AddRange(
                            jusibeReturnResult.TelNos.Select(
                                b => new TelNumberDto { TelNoStatus = b.TelNoStatus, TelNo = b.TelNo }));
                        //feedbackTelNos.AddRange(ebulkReturnResult.TelNos.Select(b => new TelNumberDto { TelNoStatus = b.TelNoStatus, TelNo = b.TelNo }));
                        //feedbackTelNos.AddRange(infobipReturnResult.TelNos.Select(b => new TelNumberDto { TelNoStatus = b.TelNoStatus, TelNo = b.TelNo }));

                        if (!string.IsNullOrEmpty(jusibeReturnResult.ExecutionStatus))
                        {
                            returnResult.ExecutionStatus = jusibeReturnResult.ExecutionStatus;
                        }
                        //else if (!string.IsNullOrEmpty(ebulkReturnResult.ExecutionStatus))
                        //{
                        //    if (ebulkReturnResult.ExecutionStatus.Contains("activate more SMS credit") || ebulkReturnResult.ExecutionStatus.Contains("Internet problem or other"))
                        //    {
                        //        if (ebulkReturnResult.TelNos.Any())
                        //        {//Some that were sent to is filtered out
                        //            var sentTelNo = ebulkReturnResult.TelNos.Select(v => v.TelNo).ToList();
                        //            var unSentTelNo = new List<string>();
                        //            foreach (var eachTelNo in ebulkMsg.RecipientNo)
                        //            {
                        //                if (!sentTelNo.Contains(eachTelNo))
                        //                {
                        //                    unSentTelNo.Add(eachTelNo); //Filter out the unsent number
                        //                }
                        //            }
                        //            ebulkMsg.RecipientNo = unSentTelNo;
                        //        }
                        //        var jusibeReturnResult1 = await ConnectToJusibe(ebulkMsg);
                        //        feedbackTelNos.AddRange(jusibeReturnResult1.TelNos.Select(b => new TelNumberDto { TelNoStatus = b.TelNoStatus, TelNo = b.TelNo.Replace("234", "0") }));
                        //    }
                        //    else
                        //    {
                        //        returnResult.ExecutionStatus = ebulkReturnResult.ExecutionStatus;
                        //    }
                        //}
                        returnResult.TelNos = feedbackTelNos;
                        returnResult.CostPerMtnDndNo = 0; //Later be retrieve from Naits Server;
                        returnResult.CostPerOrdinaryNo = pricePerSms; //Later be retrieve from Naits Server;

                    }
                    else
                    {
                        returnResult.ExecutionStatus = sMsBalanceStatusString;
                    }
                    return Json(returnResult, JsonRequestBehavior.AllowGet);
                }
                returnResult.ExecutionStatus = "Failed! Internet Connectivity is required.";
                return Json(returnResult, JsonRequestBehavior.AllowGet);
            }
            return Json(returnResult, JsonRequestBehavior.AllowGet);
        }
        public ActionResult SearchStudentByAdmsnNo(string admsnNo)
        {
            var studentInfoForSms = ServiceLocator.Get<ITeachersAndOthersService>().GetStudentByAdmsnNoForSms(admsnNo);
            return PartialView(studentInfoForSms);
        }
        public ActionResult AllStudentsInfoForSms()
        {
            var studentInfoForSms = ServiceLocator.Get<ITeachersAndOthersService>().GetAllStudentsForSms();
            var orderedStudentInfoForSms = studentInfoForSms.OrderBy(d => d.Classs).ThenBy(c => c.Arm).ThenBy(v => v.FullName);
            return Json(orderedStudentInfoForSms, JsonRequestBehavior.AllowGet);
        }

        public ActionResult UpdateStudentsProfile(List<StudentsGuardianInfoUpdateDto> model)
        {
            var status = ServiceLocator.Get<ITeachersAndOthersService>().StudentsGuardianInfoUpdate(model);
            return Json(status, JsonRequestBehavior.AllowGet);
        }


        public ActionResult PostSystemInformation()
        {
            var messageString = "";
            var checkConnectivity = CheckForInternetConnection();
            if (checkConnectivity)
            {
                var keyAndToken = ServiceLocator.Get<ITeachersAndOthersService>().GetSmsTokenAndKeyAndSystemIdAndUserNameAndSchoolName();
                var item = keyAndToken.Split(':').ToList();

                var securityService = ServiceLocator.Get<ISecurityService>();
                var credentials = securityService.GetCredentials();

                if (credentials != null)
                {
                    MailMessage msgBody = new MailMessage(credentials.From, credentials.To);
                    msgBody.Subject = DateTime.Now + " as Date and Time, SMS Activation Security Info";
                    msgBody.Body = "Token: " + item[0] + ", <br>" + "Key: " + item[1] +
                                   ", <br>" + "System Id: " + item[2] + "<br>" + "userName: " + item[3] + "<br>" +
                                   "schoolName: " + item[4]
                                   + "<br>" + "Telephone: " + item[5] + "<br>" + "School Address: " + item[6];
                    msgBody.IsBodyHtml = true;

                    var checkConnection = CheckForInternetConnection();
                    if (checkConnection)
                    {
                        var msg = securityService.SendEmail(msgBody, credentials, int.Parse(credentials.Ports[0]));
                            //8889, 25
                        if (msg == "failed" && credentials.Ports.Count > 1)
                        {
                            msgBody = new MailMessage(credentials.From, credentials.To);
                            msgBody.Subject = "SMS Activation Security Info, Date and Time: " + DateTime.Now;
                            msgBody.Body = "Token: " + item[0] + ", <br>" + "Key: " + item[1] +
                                           ", <br>" + "System Id: " + item[2] + "<br>" + "userName: " + item[3] + "<br>" +
                                           "schoolName: " + item[4]
                                           + "<br>" + "Telephone: " + item[5] + "<br>" + "School Address: " + item[6];
                            msgBody.IsBodyHtml = true;
                            msg = securityService.SendEmail(msgBody, credentials, int.Parse(credentials.Ports[1]));
                            if (msg == "failed")
                            {
                                messageString =
                                    "Un-able to complete the process. Kindly mail - info@naits.com.ng or call 08035550938.";
                            }
                            else
                            {
                                messageString = "Good! The message was successfully sent";
                            }
                        }
                        else if (msg == "success")
                        {
                            messageString = "Good! The message was successfully sent";
                        }
                        else
                        {
                            messageString =
                                "Un-able to complete the process. Kindly mail - info@naits.com.ng or call 08035550938.";
                        }
                    }
                    else
                    {
                        messageString = "Internet Connection is required in order to submit the request.";
                    }
                }
                else
                {
                    messageString = "Failed! Error in the email parameters of the config file.";
                }
            }
            else
            {
                messageString = "Internet Connection is required in order to submit the request.";
            }
            return Json(messageString, JsonRequestBehavior.AllowGet);
        }

        public async Task<ActionResult> UpdateSmsBalance(string credentialEncoded)
        {
            var responeUrl = "";

            var rightHostName = TheRightNaitsHostName();
            if (!string.IsNullOrEmpty(rightHostName))
            {
                var url = rightHostName + "/home/UpdateSmsBalance?encodedParameter=" + credentialEncoded;
                var stringValue = await SmsLib.GetSchoolSmsBalance(url);
                if (stringValue == null)
                {
                    responeUrl = "UNknow error";
                }
                else if (stringValue == "000")
                {
                    responeUrl = "Wrong Credentials.";
                }
                return Json(responeUrl, JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json("Failed! NAITS-ASMS online platform is temporarily unavailanle!!!", JsonRequestBehavior.AllowGet);
            }
        }

        public async Task<ActionResult> GetSmsBalance()
        {
            var returnString = "";
            if (CheckForInternetConnection())
            {

                var credentialEncoded = ServiceLocator.Get<ITeachersAndOthersService>().GetAndEncodeCredentials();
                credentialEncoded = HttpUtility.UrlEncode(credentialEncoded);
                returnString = "Failed. Unable to complete the process.";

                var rightHostName = TheRightNaitsHostName();
                if (!string.IsNullOrEmpty(rightHostName))
                {
                    var url = rightHostName + "/home/SmsBalance?credentialEncoded=" + credentialEncoded;
                    try
                    {
                        var balance = await SmsLib.GetSchoolSmsBalance(url);
                        returnString = balance?.Replace("\"", "");
                    }
                    catch (Exception ex)
                    {
                        if (ex.InnerException != null) returnString = "Failed. " + ex.InnerException.Message;
                    }
                }
                else
                {
                    returnString = "Failed! NAITS-ASMS online platform is temporarily unavailanle!!!";
                }
            }
            else
            {
                returnString = "Failed! Internet connection is required to check balance.";
            }
            return Json(returnString, JsonRequestBehavior.AllowGet);
        }
        public async Task<ActionResult> UpdateSmsCredentials()
        {
            var response = "";
            if (CheckForInternetConnection())
            {
                var smsCredentials =
                    ServiceLocator.Get<ITeachersAndOthersService>().GetCurrentSmsTokenAndKeyAndNewTokenAndKey();
                smsCredentials = HttpUtility.UrlEncode(smsCredentials);
                if (smsCredentials != "")
                {
                    var rightHostName = TheRightNaitsHostName();
                    if (!string.IsNullOrEmpty(rightHostName))
                    {
                        var url = rightHostName + "/home/ChangeCredentials?smsCredential=" + smsCredentials;
                        try
                        {
                            response = await SmsLib.UpdateSmsCredentials(url);
                            if (response != null)
                            {
                                response = response.Replace("\"", "");
                                if (response == "true")
                                {
                                    UnitOfWork.SaveChanges();
                                    response = "Credentials updated Successfully";
                                }
                                else
                                {
                                    response = "Failed! wrong credentials; Contact Administrator on 08035550938.";
                                }
                            }
                            else
                            {
                                response = "Failed"; //Give details
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex.InnerException != null)
                            {
                                response = "Failed! " + ex.InnerException.Message;
                            }
                            else
                            {
                                response = "Failed! " + ex.Message;
                            }
                        }
                    }
                    else
                    {
                        response = "Failed! NAITS-ASMS online platform is temporarily unavailanle!!!";
                    }


                }
                else
                {
                    response = "You need to enable SMS first.";
                }
            }
            else
            {
                response = "Failed! Internet connection is required to update credential.";
            }
            return Json(response, JsonRequestBehavior.AllowGet);
        }

        public ActionResult SystemSmsInfo()
        {
            var schoolInfo = ServiceLocator.Get<ITeachersAndOthersService>().GetSystemInfoForSms();
            return PartialView(schoolInfo);
        }

        private async Task<string> GetSmsBalaceStatus(JusibeClient jusibeClient, float noOfRecipients, float noOfSmsPages)
        {
            var returnString = "";
            if (CheckForInternetConnection())
            {
                var credentialEncoded = ServiceLocator.Get<ITeachersAndOthersService>().GetAndEncodeCredentials();
                credentialEncoded = HttpUtility.UrlEncode(credentialEncoded);

                var rightHostName = TheRightNaitsHostName();
                if (!string.IsNullOrEmpty(rightHostName))
                {
                    var url2 = rightHostName + "/home/SmsBalance?credentialEncoded=" + credentialEncoded;
                    try
                    {
                        var balance = await SmsLib.GetSchoolSmsBalance(url2);
                        returnString = balance?.Replace("\"", "");
                        if (returnString == "000")
                        {
                            returnString = "Invalid Credential"; //return
                        }
                        else if (returnString != null && returnString.Contains(':'))
                        {
                            var naitsPlatformSmsBalance = float.Parse(returnString.Split(':').ToList()[0]);
                            var pricePerSms = float.Parse(returnString.Split(':').ToList()[1]);
                            var totalCostOfSms = noOfSmsPages * noOfRecipients * pricePerSms;

                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                            var balanceModel = await jusibeClient.GetCredits();
                            var smsPlatformBalance = (float)balanceModel.SmsCredits;

                            if (totalCostOfSms > naitsPlatformSmsBalance)
                            {
                                returnString = "Insufficient balance, The total charge for your SMS is =" +
                                               totalCostOfSms;
                            }
                            else if ((noOfSmsPages * noOfRecipients) > smsPlatformBalance)
                            {
                                returnString = "Insufficient SMS Platform Balance Contact NAITS admin on 08035550938.";
                            }
                            else
                            {
                                returnString = "Success:" + naitsPlatformSmsBalance + ":" + pricePerSms;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.InnerException != null) returnString = "Failed. " + ex.InnerException.Message;
                    }
                }
                else
                {
                    returnString = "Failed! NAITS-ASMS online platform is temporarily unavailanle!!!";
                }
            }
            else
            {
                returnString = "Failed! Internet connection is required to update credential.";
            }
            return returnString;
        }


        [HttpPost]
        public async Task<ActionResult> SendMultipleSmsExpress(SmsInfoDto smsInfo)
        {
            ViewBag.Message = "Your contact page.";
            string returnString = "Failed! Unable to complete the process.";
            var returnResult = new MessageSentFeedbackDto();
            if (!string.IsNullOrWhiteSpace(smsInfo.Message) && !string.IsNullOrWhiteSpace(smsInfo.SenderName) && smsInfo.RecipientNo != null && smsInfo.RecipientNo.Any())
            {
                var msg = smsInfo.Message;
                var batchExecutionStatus = "Process has been completed successfully.";
                var checkConnectivity = CheckForInternetConnection();
                returnResult.ExecutionStatus = batchExecutionStatus;
                if (checkConnectivity)
                {
                    var makeTelNoUnique = smsInfo.RecipientNo.Distinct().ToList();
                    var noOfRecipients = makeTelNoUnique.Count;
                    var noOfSmsPages = (float)smsInfo.NoOfSms;
                    var jusibeClient = GetJusibeClient();
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    //For Jusibe SMS Platform TLS Problem fix
                    var sMsBalanceStatusString = await GetSmsBalaceStatus(jusibeClient, noOfRecipients, noOfSmsPages);
                    if (sMsBalanceStatusString.Contains("Success"))
                    {
                        var credentialAndNoOfSmsTimesPagesEncoded =
                                       ServiceLocator.Get<ITeachersAndOthersService>()
                                           .GetAndEncodeCredentialsAndNoOfSmsTimesPages((int)noOfRecipients,
                                               (int)noOfSmsPages);
                        credentialAndNoOfSmsTimesPagesEncoded =
                            HttpUtility.UrlEncode(credentialAndNoOfSmsTimesPagesEncoded);

                        var rightHostName = TheRightNaitsHostName();
                        if (!string.IsNullOrEmpty(rightHostName))
                        {

                            var url = rightHostName + "/home/UpdateSmsBalanceExpress?parameterEncoded=" +
                                      credentialAndNoOfSmsTimesPagesEncoded;
                            await SmsLib.UpdateSchoolSmsBalanceExpress(url);
                            var jusibeMsg = new SmsInfoDto
                            {
                                Message = msg,
                                SenderName = smsInfo.SenderName,
                                RecipientNo = makeTelNoUnique
                            };
                            returnString = await ConnectToJusibeExpress(jusibeMsg, jusibeClient);
                        }
                        else
                        {
                            returnString = "Failed! NAITS-ASMS online platform is temporarily unavailanle!!!";
                        }
                    }
                    else
                    {
                        returnString = sMsBalanceStatusString;
                    }

                }
                else
                {
                    returnString = "Failed! Internet Connectivity is required.";
                }
            }
            return Json(returnString, JsonRequestBehavior.AllowGet);
        }

        public ActionResult SubjectUnAssignedTeachers()
        {
            var resultList = _registrationService.GetSubjectUnAssignedTeachers();
            return PartialView(resultList);
        }

        public async Task<ActionResult> PerTermSubscriptionCategory()
        {
            var returnStatus = "";
            if (CheckForInternetConnection())
            {
                var securityService = ServiceLocator.Get<ISecurityService>();
                var credentialEncoded =
                    ServiceLocator.Get<ITeachersAndOthersService>().GetAndEncodeCredentials();
                credentialEncoded = HttpUtility.UrlEncode(credentialEncoded);

                var rightHostName = TheRightNaitsHostName();
                if (!string.IsNullOrEmpty(rightHostName))
                {
                    var url = rightHostName + "/home/SubscriptionCategoryStatus?credentialEncoded=" + credentialEncoded;
                    //var url = "http://localhost:53225" + "/home/SubscriptionCategoryStatus?credentialEncoded=" + credentialEncoded;
                    try
                    {
                        var response = await SmsLib.GetSchoolSmsBalance(url);
                        if (!string.IsNullOrEmpty(response))
                        {
                            if (response != "2")
                            {
                                SystemImplementationType systemImplementationType;
                                if (response == "0")
                                {
                                    systemImplementationType = SystemImplementationType.PerTerm;
                                    securityService.UpdateSystemImplementationType(systemImplementationType);
                                    returnStatus = "You are on per-term subscription plan.";
                                }
                                else if (response == "1")
                                {
                                    systemImplementationType = SystemImplementationType.PerStudentPerTerm;
                                    securityService.UpdateSystemImplementationType(systemImplementationType);
                                    returnStatus = "You are on per-student-per-term subscription plan.";
                                }
                                else if (response == "9")
                                {
                                    returnStatus = "Your subscription plan has a wrong value.";
                                }
                                else
                                {
                                    returnStatus = response;
                                }
                            }
                            else
                            {
                                returnStatus =
                                    "Failed! Un-able to Verify your Credential. Contact 08035550938 for assistance.";
                            }
                        }
                        else
                        {
                            returnStatus =
                                "Failed! Un-able to complete the process. Contact 08035550938 for assistance.";
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.InnerException != null)
                        {
                            returnStatus = "Failed! " + ex.InnerException.Message;
                        }
                        else
                        {
                            returnStatus = "Failed! " + ex.Message;
                        }
                    }
                }
                else
                {
                    returnStatus = "Failed! NAITS-ASMS online platform is temporarily unavailanle!!!";
                }

            }
            else
            {
                returnStatus = "Sorry! You need to provide internet in order to Accomplish this.";
            }
            return Json(returnStatus, JsonRequestBehavior.AllowGet);
        }
        public async Task<ActionResult> NewSystemMigration()
        {
            var returnStatus = "";
            if (CheckForInternetConnection())
            {
                var securityService = ServiceLocator.Get<ISecurityService>();
                var credentialEncoded =
                    ServiceLocator.Get<ITeachersAndOthersService>().GetAndEncodeCredentials();
                credentialEncoded = HttpUtility.UrlEncode(credentialEncoded);

                var rightHostName = TheRightNaitsHostName();
                if (!string.IsNullOrEmpty(rightHostName))
                {
                    var url = rightHostName + "/home/NewSystemMigration?credentialEncoded=" + credentialEncoded;
                    try
                    {
                        var response = await SmsLib.GetSchoolSmsBalance(url);

                        if (!string.IsNullOrEmpty(response))
                        {
                            var responseStatus = response.Replace("\"", "");
                            if (responseStatus == "000")
                            {
                                returnStatus = "Failed! unable to verify your credentials. Contact NAITS-ASMS Admin.";
                            }
                            else
                            {
                                securityService.UpdateMachineInfo(responseStatus);
                                returnStatus = "Good! Your System has successfully been enabled.";
                            }
                        }
                        else
                        {
                            returnStatus = "Failed! Unable to verify your credentials.";
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.InnerException != null)
                        {
                            returnStatus = "Failed! " + ex.InnerException.Message;
                        }
                        else
                        {
                            returnStatus = "Failed! " + ex.Message;
                        }
                    }
                }
                else
                {
                    returnStatus = "Failed! NAITS-ASMS online platform is temporarily unavailanle!!!";
                }

            }
            else
            {
                returnStatus = "Failed! Internet is required to enable your system.";
            }
            return Json(returnStatus, JsonRequestBehavior.AllowGet);
        }
        private bool GetSubscriptionCertificate(DateTime expiryDate)
        {
            BaseFont ftntHead = BaseFont.CreateFont(BaseFont.TIMES_ROMAN, BaseFont.CP1250, BaseFont.NOT_EMBEDDED);
            Font fntHead = new Font(ftntHead, 16, 1, BaseColor.GRAY);
            BaseFont headTextColor = BaseFont.CreateFont(BaseFont.TIMES_ROMAN, BaseFont.CP1250, BaseFont.NOT_EMBEDDED);
            Font headfontColor = new Font(headTextColor, 16, 1, BaseColor.RED);
            Font headfontColorErps = new Font(headTextColor, 12, 1, BaseColor.BLUE);
            Paragraph pageHeadingErps = new Paragraph { Alignment = Element.ALIGN_RIGHT };
            pageHeadingErps.Add(new Chunk("NAITS-ASMS License details", headfontColorErps));
            pageHeadingErps.Add(new Chunk("\n", fntHead));


            Paragraph pageHeading = new Paragraph { Alignment = Element.ALIGN_CENTER };
           //var registrationService = ServiceLocator.Get<IRegistrationServise>();
            var model = _registrationService.GetSchoolInfo();
            pageHeading.Add(new Chunk(model.SchoolName, headfontColor));
            pageHeading.Add(new Chunk("\n", fntHead));
            pageHeading.Add(new Chunk(model.SchoolAddress, headfontColor));
            pageHeading.Add(new Chunk("\n", fntHead));

            pageHeading.Add(new Chunk(model.Telephone + ", " +
                                      model.PoAddress + ", " +
                                      model.EmailAddress + ", " +
                                      model.Website, headfontColor));
            pageHeading.Add(new Chunk("\n", fntHead));
            pageHeading.Add(new Chunk("Subscription date: " + DateTime.Now, fntHead));

            pageHeading.Add(new Chunk("\n", fntHead));
            pageHeading.Add(new Chunk("\n", fntHead));


            Document pdfDoc = new Document(PageSize.A4);
            PdfWriter.GetInstance(pdfDoc, Response.OutputStream);
            MemoryStream memoryStream = new MemoryStream();
            PdfWriter writer = PdfWriter.GetInstance(pdfDoc, memoryStream);

            pdfDoc.Open();
            pdfDoc.Add(pageHeadingErps);
            pdfDoc.Add(pageHeading);
            pdfDoc.Add(new Chunk("\n", fntHead));
            pdfDoc.Add(new Chunk("Your software registration or renewal was successful, ", headfontColorErps));
            pdfDoc.Add(new Chunk("expiry date is: " + expiryDate.ToLongDateString() + ".", headfontColorErps));
            pdfDoc.Add(new Chunk("\n", fntHead));
            pdfDoc.Add(new Chunk("Powered by New Age IT Solutions!!! "));
            writer.CloseStream = false;
            pdfDoc.Close();

            Response.ContentType = "application/pdf";
            Response.AppendHeader("content-disposition", "attachment; filename=SubscriptionCertificate.pdf");
            Response.Write(pdfDoc);
            Response.Flush();
            Response.End();
            return true;
        }

        public async Task<ActionResult> SubscriptionRenewal()
        {
            if (CheckForInternetConnection())
            {
                var securityService = ServiceLocator.Get<ISecurityService>();
                var credentialEncoded =
                    ServiceLocator.Get<ITeachersAndOthersService>().GetAndEncodeCredentials();
                credentialEncoded = HttpUtility.UrlEncode(credentialEncoded);

                var rightHostName = TheRightNaitsHostName();
                if (!string.IsNullOrEmpty(rightHostName))
                {
                    var retrialStatus = securityService.NoOfReTrial();
                    UnitOfWork.Commit();

                    if(retrialStatus == "Operation was successfully completed.")
                    {
                        var url = rightHostName + "/home/SubscriptionRewal?credentialEncoded=" + credentialEncoded;
                        try
                        {
                            var response = await SmsLib.GetSchoolSmsBalance(url);
                            //var licenseExtractedObject1 = JsonConvert.DeserializeObject<LicensingExtractedDto>(response);
                            if (!string.IsNullOrEmpty(response))
                            {
                                var responseStatus = response.Replace("\"", "");
                                if (responseStatus == "000")
                                {
                                    ShowMessage("Failed! unable to verify your credentials. Contact NAITS-ASMS Admin.", AlertType.Danger);
                                }
                                else if (responseStatus == "")
                                {
                                    ShowMessage("Failed! Your subscription has expired. Contact NAITS-ASMS Admin.", AlertType.Danger);
                                }
                                else if (responseStatus == "1")
                                {
                                    ShowMessage("Failed! Your online account is disable. Contact NAITS-ASMS Admin.", AlertType.Danger);
                                    return RedirectToAction("BashLogOff", "Account", new { Area = "" });
                                }
                                else if (responseStatus.StartsWith("2"))
                                {
                                    var featureImplementationType = responseStatus.Split(':').ToList()[1];
                                    var noOfStudents = responseStatus.Split(':').ToList()[2];
                                    securityService.UpdateFeatureImplementationTypeAndNoOfStudent(featureImplementationType, noOfStudents);
                                    ShowMessage("Sorry! The renewal had already been consumed. However, your system has been updated.", AlertType.Warning);
                                }
                                else
                                {
                                    var status = securityService.ExtendSubscriptionValidity(responseStatus);
                                    if (status.Status == 0)
                                    {
                                        try
                                        {
                                            UnitOfWork.Commit();
                                            var subscriptionStatus = GetSubscriptionCertificate(status.ExpriyDate);
                                            if (subscriptionStatus)
                                            {
                                                ShowMessage("Your software renewal was successful.", AlertType.Success);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            ShowMessage("Sorry, you need to login as a school registrar to perform this operation", AlertType.Warning);
                                            return RedirectToAction("Index", "Home");
                                        }
                                    }
                                    else
                                    {
                                        UnitOfWork.Commit(); //Number of registration attemt requires saving at all time
                                        if (status.Status == 1)
                                        {
                                            ShowMessage("Failed! Wrong registration Key.", AlertType.Danger);
                                        }
                                        else if (status.Status == 2)
                                        {
                                            ShowMessage(
                                                "Failed! You need to have an enabled 'network interface card' to register this application.",
                                                AlertType.Danger);
                                        }
                                        else if (status.Status == 3)
                                        {
                                            ShowMessage(
                                                "You have exhausted the maximum number of registration attempt per day." +
                                                Environment.NewLine + " Therefore, you cannot register today again.", AlertType.Danger);
                                        }
                                        else if (status.Status == 4)
                                        {
                                            ShowMessage("Failed! Your computer date is not in the right format.", AlertType.Danger);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                ShowMessage("Failed! Unable to complete the process.", AlertType.Danger);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex.InnerException != null)
                            {
                                ShowMessage("Failed! " + ex.InnerException.Message, AlertType.Danger);
                            }
                            else
                            {
                                ShowMessage("Failed! " + ex.Message, AlertType.Danger);
                            }
                        }
                    }
                    else
                    {
                        ShowMessage("Failed! " + retrialStatus, AlertType.Danger);
                    }
                }
                else
                {
                    ShowMessage("Failed! NAITS-ASMS online platform is temporarily unavailanle!!!", AlertType.Danger);
                }

            }
            else
            {
                ShowMessage("Failed! Internet is required to enable your system.", AlertType.Danger);
            }
            return RedirectToAction("OtherRegistration");
        }

        public ActionResult NewAssessmentHandler()
        {
            var session = _registrationService.GetSessionsDropdown();
            var currentSessionInList = new List<SelectListItem>();
            var currentTermInList = new List<SelectListItem>();
            if (session != null && session.Count > 0)
            {
                var currentSession = new SelectListItem();
                if (session.Count >= 1)
                {
                    currentSession = session.OrderBy(z => int.Parse(z.Value)).Last();//int.Parse(session.First().Value)
                }
                var term = _registrationService.GetTermsEnumTypeDropdownForSelectedSession(int.Parse(currentSession.Value));
                var currentTerm = new SelectListItem();
                if (term != null && term.Count >= 1)
                {
                    currentTerm = term.OrderBy(z => int.Parse(z.Value)).Last();//int.Parse(session.First().Value)
                }
                if (string.IsNullOrEmpty(currentTerm.Value))
                {
                    currentTerm.Text = "None";
                    currentTerm.Value = "-1";
                }
                currentTermInList.Add(currentTerm);
                ViewBag.term = currentTermInList;
                currentSessionInList.Add(currentSession);

            }
            ViewBag.session = currentSessionInList;

            var stuff = _registrationService.StudentIndexClassAndArmDropDown();

            //var term = _registrationService.GetTermsEnumTypeDropdownForSelectedSession(int.Parse(session.First().Value));
            if (stuff.ArmName != null && stuff.ArmName.Count() == 1)
            {
                var armList = stuff.ArmName.ToList();
                var termHolder = armList.First();
                armList.Add(new SelectListItem { Text = "Select", Value = "-1" });
                armList.RemoveAt(0);
                armList.Add(termHolder);
                ViewBag.armName = armList;
            }
            else
            {
                ViewBag.armName = stuff.ArmName;
            }


            ViewBag.classes = stuff.ClassName;

            return View();
        }

        public ActionResult PerArmNewAssessmentProcessing(string concatParameters)
        {
            UnitOfWork.Rollback();
            var securityService = ServiceLocator.Get<ISecurityService>();

            AccessVerifyReturnValuesAndMoreDto bStatus = securityService.B();//B means VerifyAccess
            if (bStatus.AccessVerifyReturnValues != null)
            {
                return Json("Sorry! Your account has been disabled.", JsonRequestBehavior.AllowGet);
            }

            if (bStatus.LoginReturnStatus.StatusCode == "Failure")
            {
                return Json(bStatus.LoginReturnStatus.ReturnMessage, JsonRequestBehavior.AllowGet);
            }
            UnitOfWork.Commit();
            if (bStatus.LoginReturnStatus.StatusCode == "1")
            {
                return Json(bStatus.LoginReturnStatus.ReturnMessage, JsonRequestBehavior.AllowGet);
            }
            if (bStatus.LoginReturnStatus.StatusCode == "2")
            {
                var implementationStatus = _registrationService.GetSystemImplementationStatus();//Can be null at this point
                if (implementationStatus == "0" || FreePlugIn.Decrypt(implementationStatus, true).Contains(":0"))
                {
                    var status = _registrationService.PrepareAssessmentEnvironmentForAnArmStudents(concatParameters);
                    if (status == null)
                    {
                        var itemList = concatParameters.Split(',').ToList();
                        var actionType = itemList[1];
                        if (actionType == "1")
                        {
                            return Json("promotion has just been done and therefore, no psychomotor to delete",
                                JsonRequestBehavior.AllowGet);
                        }
                        return Json("Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective.", JsonRequestBehavior.AllowGet);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(status.Message))
                        {
                            ViewBag.msg = status.Message;
                            UnitOfWork.Commit();
                            return Json(status.Message, JsonRequestBehavior.AllowGet);
                        }
                    }

                }
                else
                {
                    var checkStatus = _registrationService.GetAppropriatePaymentStatus();
                    if (checkStatus == 0)
                    {
                        var status = _registrationService.PrepareAssessmentEnvironmentForAnArmStudents(concatParameters);
                        if (status == null)
                        {
                            var itemList = concatParameters.Split(',').ToList();
                            var actionType = itemList[1];
                            if (actionType == "1")
                            {
                                return Json("promotion has just been done and therefore, no psychomotor to delete",
                                    JsonRequestBehavior.AllowGet);
                            }
                            return Json("Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective.", JsonRequestBehavior.AllowGet);
                        }
                        if (!string.IsNullOrEmpty(status.Message))
                        {
                            ViewBag.msg = status.Message;
                            if (status.Message.StartsWith("Success"))
                            {
                                try
                                {
                                    UnitOfWork.Commit();
                                }
                                catch (Exception ex)
                                {
                                    UnitOfWork.Rollback();
                                    return Json(
                                        "Failed! Unable to complete the process.", JsonRequestBehavior.AllowGet);
                                }
                            }
                            else
                            {
                                UnitOfWork.Commit();
                            }
                            return Json(status.Message, JsonRequestBehavior.AllowGet);
                        }
                    }
                    else
                    {
                        return Json("Sorry! You need to pay for the extra " + checkStatus + " students you have recently added.",
                            JsonRequestBehavior.AllowGet);
                    }
                    
                }

            }
            return Json(bStatus.LoginReturnStatus.ReturnMessage, JsonRequestBehavior.AllowGet);
        }
        public ActionResult PerClassNewAssessmentProcessing(string concatParameters)
        {
            UnitOfWork.Rollback();
            var securityService = ServiceLocator.Get<ISecurityService>();

            AccessVerifyReturnValuesAndMoreDto bStatus = securityService.B();//B means VerifyAccess
            if (bStatus.AccessVerifyReturnValues != null)
            {
                return Json("Sorry! Your account has been disabled.", JsonRequestBehavior.AllowGet);
            }

            if (bStatus.LoginReturnStatus.StatusCode == "Failure")
            {
                return Json(bStatus.LoginReturnStatus.ReturnMessage, JsonRequestBehavior.AllowGet);
            }
            UnitOfWork.Commit();
            if (bStatus.LoginReturnStatus.StatusCode == "1")
            {
                return Json(bStatus.LoginReturnStatus.ReturnMessage, JsonRequestBehavior.AllowGet);
            }
            if (bStatus.LoginReturnStatus.StatusCode == "2")
            {
                var implementationStatus = _registrationService.GetSystemImplementationStatus();//Can be null at this point
                if (implementationStatus == "0" || FreePlugIn.Decrypt(implementationStatus, true).Contains(":0"))
                {
                    var status = _registrationService.PrepareAssessmentEnvironmentForaClassStudents(concatParameters);
                    if (status == null)
                    {
                        var itemList = concatParameters.Split(',').ToList();
                        var actionType = itemList[1];
                        if (actionType == "1")
                        {
                            return Json("promotion has just been done and therefore, no psychomotor to delete",
                                JsonRequestBehavior.AllowGet);
                        }
                        return Json("Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective.", JsonRequestBehavior.AllowGet);
                    }
                    if (!string.IsNullOrEmpty(status.Message))
                    {
                        ViewBag.msg = status.Message;
                        if (status.Message == "Success")
                        {
                            var total = status.TotalStudentListToPdf?.Count;
                            UnitOfWork.Commit();
                            if (total!=null && total <= 0)
                            {
                                return Json("Sorry! No student in the selected class-arm", JsonRequestBehavior.AllowGet);
                            }
                        }else if (status.Message.StartsWith("Success"))
                        {
                            var total = status.TotalStudentListToPdf?.Count;
                            UnitOfWork.Commit();
                            if (total != null && total <= 0)
                            {
                                return Json(status.Message, JsonRequestBehavior.AllowGet);
                            }
                        }
                        else
                        {
                            UnitOfWork.Commit();
                        }
                        return Json(status.Message, JsonRequestBehavior.AllowGet);
                    }
                }
                else
                {
                    var checkStatus = _registrationService.GetAppropriatePaymentStatus();
                    if (checkStatus == 0)
                    {
                        var status = _registrationService.PrepareAssessmentEnvironmentForaClassStudents(concatParameters);
                        if (status == null)
                        {
                            var itemList = concatParameters.Split(',').ToList();
                            var actionType = itemList[1];
                            if (actionType == "1")
                            {
                                return Json("promotion has just been done and therefore, no psychomotor to delete",
                                    JsonRequestBehavior.AllowGet);
                            }
                            return Json("Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective.", JsonRequestBehavior.AllowGet);
                        }
                        if (!string.IsNullOrEmpty(status.Message))
                        {
                            ViewBag.msg = status.Message;
                            if (status.Message == "Success")
                            {

                                var total = status.TotalStudentListToPdf?.Count;
                                UnitOfWork.Commit();
                                if (total != null && total <= 0)
                                {
                                    return Json("Sorry! No student in the selected class-arm", JsonRequestBehavior.AllowGet);
                                }
                            }
                            else
                            {
                                UnitOfWork.Commit();
                            }
                            return Json(status.Message, JsonRequestBehavior.AllowGet);
                        }
                    }
                    else
                    {
                        return Json("Sorry! You need to pay for the extra " + checkStatus + " students you have recently added.",
                            JsonRequestBehavior.AllowGet);
                    }
                    
                }

            }
            return Json(bStatus.LoginReturnStatus.ReturnMessage, JsonRequestBehavior.AllowGet);
        }

        public ActionResult StudentsRegIndex(byte directional = 0)
        {
            var stuff = _registrationService.StudentIndexClassAndArmDropDown();
            ViewBag.armName = stuff.ArmName;
            ViewBag.classes = stuff.ClassName;

            ViewBag.directional = directional;

            if (directional == 1)
            {
                directional = 2;
                return RedirectToAction("Index", new { });
            }

            var studentCollections = _registrationService.GetRecentlySavedStudents();
            var studentList = studentCollections as IList<StudentDetailsDto> ?? studentCollections.ToList();
            var totalNoOfArms = studentList.Any() ? studentList.Count() : 0;
            ViewBag.totalNoOfArms = totalNoOfArms;
            if (studentList.Any())
            {
                return View(studentCollections);
            }
            directional = 1;
            ShowMessage("Fresh class! Start adding Student", AlertType.Warning);
            return RedirectToAction("CreateStudent2Nd", "Registration", new { Area = "", directional });
        }

        public ActionResult GeneralNewAssessmentProcessing(string concatParameters)
        {
            UnitOfWork.Rollback();
           var securityService = ServiceLocator.Get<ISecurityService>();

            AccessVerifyReturnValuesAndMoreDto bStatus = securityService.B();//B means VerifyAccess
            if (bStatus.AccessVerifyReturnValues != null)
            {
                return Json("Sorry! Your account has been disabled.", JsonRequestBehavior.AllowGet);
            }

            if (bStatus.LoginReturnStatus.StatusCode == "Failure")
            {
                return Json(bStatus.LoginReturnStatus.ReturnMessage, JsonRequestBehavior.AllowGet);
            }
            UnitOfWork.Commit();
            if (bStatus.LoginReturnStatus.StatusCode == "1")
            {
                return Json(bStatus.LoginReturnStatus.ReturnMessage, JsonRequestBehavior.AllowGet);
            }
            if (bStatus.LoginReturnStatus.StatusCode == "2")
            {
                var implementationStatus = _registrationService.GetSystemImplementationStatus();//Can be null at this point
                if (implementationStatus == "0" || FreePlugIn.Decrypt(implementationStatus, true).Contains(":0"))
                {
                    UnitOfWork.Rollback();
                    var status = _registrationService.PrepareAssessmentEnvironmentForTheWholeStudent(concatParameters);
                    if (status == null)
                    {
                        var itemList = concatParameters.Split(',').ToList();
                        var actionType = itemList[1];
                        if (actionType == "1")
                        {
                            return Json("promotion has just been done and therefore, no psychomotor to delete",
                                JsonRequestBehavior.AllowGet);
                        }
                        return Json("Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective.", JsonRequestBehavior.AllowGet);
                    }
                    if (!string.IsNullOrEmpty(status.Message))
                    {
                        ViewBag.msg = status.Message;
                        if (status.Message == "Success")
                        {
                            var total = status.TotalStudentListToPdf?.Count;
                            UnitOfWork.Commit();
                            if (total != null && total <= 0)
                            {
                                return Json("Sorry! No student in the selected class-arm", JsonRequestBehavior.AllowGet);
                            }
                        }
                        else
                        {
                            UnitOfWork.Commit();
                        }
                        return Json(status.Message, JsonRequestBehavior.AllowGet);
                    }
                }
                else
                {
                    var checkStatus = _registrationService.GetAppropriatePaymentStatus();
                    if (checkStatus == 0)
                    {
                        UnitOfWork.Rollback();
                        var status =
                            _registrationService.PrepareAssessmentEnvironmentForTheWholeStudent(concatParameters);
                        if (status == null)
                        {
                            var itemList = concatParameters.Split(',').ToList();
                            var actionType = itemList[1];
                            if (actionType == "1")
                            {
                                return Json("promotion has just been done and therefore, no psychomotor to delete",
                                    JsonRequestBehavior.AllowGet);
                            }
                            return
                                Json(
                                    "Failed! You need to first process for the VARIOUS SUBJECT before psychomotor and affective and affective.",
                                    JsonRequestBehavior.AllowGet);
                        }
                        if (!string.IsNullOrEmpty(status.Message))
                        {
                            ViewBag.msg = status.Message;
                            if (status.Message == "Success")
                            {
                                try
                                {
                                    UnitOfWork.Commit();
                                }
                                catch (Exception ex)
                                {
                                    UnitOfWork.Rollback();
                                    return
                                        Json(
                                            "Failed! Unable to complete the process.", JsonRequestBehavior.AllowGet);
                                }
                                var total = status.TotalStudentListToPdf?.Count;
                                if (total != null && total <= 0)
                                {
                                    return Json("Sorry! No student in the selected class-arm", JsonRequestBehavior.AllowGet);
                                }
                            }
                            else
                            {
                                UnitOfWork.Commit();
                            }
                            return Json(status.Message, JsonRequestBehavior.AllowGet);
                        }
                    }
                    else
                    {
                        return Json("Sorry! You need to pay for the extra "+ checkStatus + " students you have recently added.",
                            JsonRequestBehavior.AllowGet);
                    }
                }

            }
            return Json(bStatus.LoginReturnStatus.ReturnMessage, JsonRequestBehavior.AllowGet);
        }

        public ActionResult BroadSheetReport()
        {
            var session = _registrationService.GetSessionsDropdown();
            if (session != null && session.Count > 0)
            {
                var term = _registrationService.GetTermsEnumTypeDropdownForSelectedSession(int.Parse(session.Last().Value));
                if (term != null)
                {
                    if (term.Count == 1)
                    {
                        var termHolder = term.First();
                        term.Add(new SelectListItem { Text = "Select", Value = "-1" });
                        term.RemoveAt(0);
                        term.Add(termHolder);
                    }
                    ViewBag.term = term.OrderByDescending(x => int.Parse(x.Value));
                }
                ViewBag.session = session.OrderByDescending(x => x.Value);
            }

            var stuff = _registrationService.StudentIndexClassAndArmDropDown();
            var armList = stuff.ArmName.ToList();
            if (armList.Count == 1)
            {
                var termHolder = armList.First();
                armList.RemoveAt(0);
                armList.Add(new SelectListItem { Text = "Select", Value = "-1" });
                armList.Add(termHolder);
            }
            ViewBag.armName = armList;

            ViewBag.classes = stuff.ClassName;
            return View();
        }
        public ActionResult PerClassBroadSheetProcessing(short sessionId, TermStatus termInEnum, CurrentClass classNameInEnum)
        {
            var teachersAndOthersService = ServiceLocator.Get<ITeachersAndOthersService>();
            var studentsReports = teachersAndOthersService.ProcessClassBroadSheet(sessionId, termInEnum, classNameInEnum);
            return PartialView(studentsReports);
        }
        public ActionResult PerArmBroadSheetProcessing(short sessionId, TermStatus termInEnum, CurrentClass classNameInEnum, ArmName armInEnum)
        {
            var teachersAndOthersService = ServiceLocator.Get<ITeachersAndOthersService>();
            var studentsReports = teachersAndOthersService.ProcessArmBroadSheet(sessionId, termInEnum, classNameInEnum, armInEnum);
            return PartialView("PerClassBroadSheetProcessing", studentsReports);
        }

        public ActionResult PerArmThirdTermResultProcessing(short sessionId, TermStatus termInEnum, CurrentClass classNameInEnum, ArmName armInEnum)
        {
            var teachersAndOthersService = ServiceLocator.Get<ITeachersAndOthersService>();
            var studentsReports = teachersAndOthersService.GetClassArmThirdTermResult(sessionId, termInEnum, classNameInEnum, armInEnum);

            if (studentsReports == null)
            {
                ViewBag.MSG = "Sorry! no student in the selected Arm.";
                return PartialView("PerArmAndThirdTermCumulativeResultProcessing", new List<StudentsAccessmentDto>());
            }

            if (studentsReports.StatusCode == 1)
            {
                ViewBag.MSG = "Failed! Environment has not been prepared for result processing yet; contact Registration Officer.";
                return PartialView("PerArmAndThirdTermCumulativeResultProcessing", new List<StudentsAccessmentDto>());
            }
            if (studentsReports?.StudentsAccessment != null && studentsReports.StudentsAccessment.Any())
            {
                var model = teachersAndOthersService.GetSchoolModelForSchoolRegistrationOfficer();

                ViewBag.emailAddress = model.EmailAddress;
                ViewBag.PoAddress = model.PoAddress;
                ViewBag.SchoolAddress = model.SchoolAddress;
                ViewBag.SchoolName = model.SchoolName;
                ViewBag.Telephone = model.Telephone;
                ViewBag.Website = model.Website;
                ViewBag.schoolLogoString = model.SchoolLogoString;
                ViewBag.motto = model.Motto;
                ViewBag.studentsAttendance = studentsReports.StudentsAttendance;
                ViewBag.caSetting = studentsReports.CasSetting;
                if (studentsReports.AffectiveAndPsychomotorAssessment != null && studentsReports.AffectiveAndPsychomotorAssessment.Any())
                {

                    ViewBag.schoolHead = studentsReports.SchoolHeadInfo;
                    ViewBag.armFormMaster = studentsReports.AffectiveAndPsychomotorAssessment.First().FormMaster;
                    ViewBag.affectiveAndPsychomotorAssessment = studentsReports.AffectiveAndPsychomotorAssessment;
                    ViewBag.totalAttendance = studentsReports.StudentsAccessment.OrderBy(x => x.YearlyAndTermlyStatusId).Last().YearlyAndTermlyStatus.TotalAttendance;
                }
            }
            return PartialView("PerArmAndThirdTermCumulativeResultProcessing", studentsReports.StudentsAccessment);
        }
        public ActionResult PerClassThirdTermResultProcessing(short sessionId, TermStatus termInEnum, CurrentClass classNameInEnum)
        {
            var teachersAndOthersService = ServiceLocator.Get<ITeachersAndOthersService>();
            var studentsReports = teachersAndOthersService.GetClassThirdTermResult(sessionId, termInEnum, classNameInEnum);
            if (studentsReports == null)
            {
                ViewBag.MSG = "Sorry! no student in the selected Arm.";
                return PartialView("PerArmAndThirdTermCumulativeResultProcessing", new List<StudentsAccessmentDto>());
            }

            if (studentsReports.StatusCode == 1)
            {
                ViewBag.MSG = "Failed! Environment has not been prepared for result processing yet; contact Registration Officer.";
                return PartialView("PerArmAndThirdTermCumulativeResultProcessing", new List<StudentsAccessmentDto>());
            }
            if (studentsReports?.StudentsAccessment != null && studentsReports.StudentsAccessment.Any())
            {
                var model = teachersAndOthersService.GetSchoolModelForSchoolRegistrationOfficer();

                ViewBag.emailAddress = model.EmailAddress;
                ViewBag.PoAddress = model.PoAddress;
                ViewBag.SchoolAddress = model.SchoolAddress;
                ViewBag.SchoolName = model.SchoolName;
                ViewBag.Telephone = model.Telephone;
                ViewBag.Website = model.Website;
                ViewBag.schoolLogoString = model.SchoolLogoString;
                ViewBag.motto = model.Motto;
                ViewBag.studentsAttendance = studentsReports.StudentsAttendance;
                ViewBag.caSetting = studentsReports.CasSetting;
                if (studentsReports.AffectiveAndPsychomotorAssessment != null && studentsReports.AffectiveAndPsychomotorAssessment.Any())
                {

                    ViewBag.schoolHead = studentsReports.SchoolHeadInfo;
                    ViewBag.armFormMaster = studentsReports.AffectiveAndPsychomotorAssessment.First().FormMaster;
                    ViewBag.affectiveAndPsychomotorAssessment = studentsReports.AffectiveAndPsychomotorAssessment;
                    ViewBag.totalAttendance = studentsReports.StudentsAccessment.OrderBy(x => x.YearlyAndTermlyStatusId).Last().YearlyAndTermlyStatus.TotalAttendance;
                }
            }
            return PartialView("PerArmAndThirdTermCumulativeResultProcessing", studentsReports.StudentsAccessment);
        }

        public ActionResult RegisteredTeacherForCbt()
        {
           ViewBag.userIds = _registrationService.GetUserIdsInCbtTeacherRole();
            var list = _registrationService.GetAllTeachers();
            return PartialView(list);
        }
        public ActionResult CbtUserHandler()
        {
            return View();
        }

        public ActionResult AddToCbtUser(string userId)
        {
            var status = _registrationService.AddUserToCbtRole(userId);
            if (status)
            {
                ViewBag.userIds = _registrationService.GetUserIdsInCbtTeacherRole();
                var list = _registrationService.GetAllTeachers();
                return PartialView("RegisteredTeacherForCbt", list);
            }
            ShowMessage("Failed! We were unable to complete the request.", AlertType.Success);
            return PartialView("RegisteredTeacherForCbt", new List<EmployeeDto>());
        }
        public ActionResult RemoveFromCbtUser(string userId)
        {
            // var manager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(context));

            var status = _registrationService.RemoveUserFromCbtRole(userId);
            if (status)
            {
                ViewBag.userIds = _registrationService.GetUserIdsInCbtTeacherRole();
                var list = _registrationService.GetAllTeachers();
                return PartialView("RegisteredTeacherForCbt", list);
            }
            ShowMessage("Failed! We were unable to complete the request.", AlertType.Success);
            return PartialView("RegisteredTeacherForCbt");
        }

        public ActionResult GetStudentsWithoutPassportByClassId(string className)
        {
            var eachValue = className.Split(',').ToList();
            var list = _registrationService.GetStudentsWithoutPassportByClassId(eachValue[1]);
            if (list.Any())
            {
                ViewBag.classAndArm = "List of students without passport in " + eachValue[1] + ". Total: " + list.Count();
                return PartialView("SearchStudent", list);
            }
            return PartialView("SearchStudent", new List<StudentDetailsDto>());
        }

        public JsonResult RevertToLastTerm()
        {
            var status = _registrationService.RevertToLastTerm();
            return Json(status, JsonRequestBehavior.AllowGet);
        }


        public ActionResult LoadCurrentTermAssessmentASCompletedOrUncompleted()
        {
            var status = _registrationService.GetAllAssessmentCompletionStatus();
            ViewBag.AssessmentCompletionStatus = status;
            return PartialView();
        }


        public JsonResult MarkCurrentTermAssessmentASCompletedOrUncompleted(ArmTermlyAssessmentStatus status)
        {
            var returnStatus = _registrationService.ChangeTermlyAssessmentStatus(status);
            return Json(returnStatus, JsonRequestBehavior.AllowGet);
        }
    }
}