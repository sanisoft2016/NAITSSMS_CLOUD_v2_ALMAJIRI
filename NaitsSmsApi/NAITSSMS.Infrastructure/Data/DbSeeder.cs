using CustRewardMgtSys.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Data.Security;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NAITSSMS.Infrastructure.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;

            var db = sp.GetRequiredService<AppDbContext>();
            var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

            await db.Database.MigrateAsync();
            await SchemaRepair.ApplyAsync(db);

           // if (db.Users.Any())
           // {
           //     return;
           // }
           // // Regions
           // var regions = "1-Northern Central,2-North East,3-North West,4-South East,5-South South,6-South West"
           //     .Split(',').ToList();
           // foreach (var r in regions)
           // {
           //     var parts = r.Split('-');
           //     var id = byte.Parse(parts[0]);
           //     var name = parts[1];
           //     if (!await db.Regions.AnyAsync(s => s.Name == name))
           //     {
           //         db.Regions.Add(new Region { Id = id, Name = name });
           //     }
           // }
           // await db.SaveChangesAsync();

           // // States
           // var states = "1-Abia,2-Adamawa,3-Akwa Ibom,4-Anambra,5-Bauchi,6-Bayelsa,7-Benue,8-Borno,9-Cross River,10-Delta,11-Ebonyi,12-Edo,13-Ekiti,14-Enugu,15-FCT,16-Gombe,17-Imo,18-Jigawa,19-Kaduna,20-Kano,21-katsina,22-Kebbi,23-Kogi,24-Kwara,25-Lagos,26-Nasarawa,27-Niger,28-Ogun,29-Ondo,30-Osun,31-Oyo,32-Plateau,33-Rivers,34-Sokoto,35-Taraba,36-Yobe,37-Zamfara,38-Provide State Later"
           //     .Split(',').ToList();
           // var regionstates = new List<string>
           // {
           //     "1,7","1,23","1,24","1,26","1,27","1,15","1,32","2,2","2,5","2,8","2,16","2,35","2,36","3,18","3,19","3,20","3,21","3,22","3,34","3,37","4,1","4,4","4,11","4,14","4,17","5,3","5,9","5,33","5,6","5,10","5,12","6,13","6,25","6,28","6,29","6,30","6,31","6,38"
           // };
           // foreach (var state in states)
           // {
           //     var stateParts = state.Split('-');
           //     var stateId = stateParts[0];
           //     var stateName = stateParts[1];
           //     string regionId = "";
           //     foreach (var regionState in regionstates)
           //     {
           //         var rsParts = regionState.Split(',');
           //         if (stateId == rsParts[1])
           //         {
           //             regionId = rsParts[0];
           //             break;
           //         }
           //     }
           //     if (!await db.States.AnyAsync(s => s.Name == stateName))
           //     {
           //         db.States.Add(new State
           //         {
           //             Id = byte.Parse(stateId),
           //             Name = stateName,
           //             RegionId = byte.Parse(regionId)
           //         });
           //     }
           // }
           // await db.SaveChangesAsync();

           // var lgas = new[]
           // {
           //     "1,Aba North","1,Aba South","1,Arochukwu","1,Bende","1,Ikwuano","1,Isiala-Ngwa North","1,Isiala-Ngwa South",
           //     "1,Isikwuato","1,Nneochi","1,Obi Ngwa","1,Obioma Ngwa","1,Ohafia","1,Ohaozara","1,Osisioma","1,Ugwunagbo",
           //     "1,Ukwa West","1,Ukwu East","1,Umuahia North","1,Umuahia South","1,Umunneochi","1,Provide LGA Later","2,Demsa",
           //     "2,Fufore","2,Ganye","2,Girei","2,Gombi","2,Guyuk","2,Hong","2,Jada","2,Jimeta","2,Lamurde","2,Madagali",
           //     "2,Maiha","2,Mayo-Belwa","2,Michika","2,Mubi-North","2,Mubi-South","2,Numan","2,Shelleng","2,Song","2,Toungo",
           //     "2,Yola","2,Provide LGA Later","3,Abak","3,Eastern-Obolo","3,Eket","3,Ekpe-Atani","3,Esit Ekit","3,Essien-Udim",
           //     "3,Etim-Ekpo","3,Etinam","3,Ibeno","3,Ibesikpo-Asutan","3,Ibiono-Ibom","3,Ika","3,Ikono","3,Ikot-Abasi","3,Ikot-Ekpene",
           //     "3,Ini","3,Itu","3,Mbo","3,Mkpat-Enin","3,Nsit-Ibom","3,Nsit-Ubium","3,Obot-Akara","3,Okobo","3,Onna","3,Oron",
           //     "3,Oruk Anam","3,Udung-Uko","3,Ukanefun","3,Uquo Ibeno","3,Uru Offong Oruko","3,Uruan","3,Uruk-Anam","3,Uyo",
           //     "3,Provide LGA Later","4,Aguata","4,Akwa North","4,Anambra","4,Anambra-West","4,Anaocha","4,Awka-North",
           //     "4,Awka-South","4,Ayamelum","4,Dunukofia","4,Ekwusigo","4,Idemili","4,Idemili-North","4,Idemili-South",
           //     "4,Ihiala","4,Imo","4,Nibo","4,Njikoka","4,Nnewi","4,Nnewi-North","4,Nnewi-South","4,Ogbaru","4,Olumba",
           //     "4,Onitsha-North","4,Onitsha-South","4,Orumba-North","4,Orumba-South","4,Oti","4,Otu-Ocha","4,Ubuluizor Ihiala",
           //     "4,Uyi","4,Provide LGA Later","5,Alkaleri","5,Bauchi","5,Bogoro","5,Damban","5,Darazo","5,Dass","5,Gamawa","5,Ganjuwa",
           //     "5,Giade","5,Itas/Gadau","5,Provide LGA Later","6,Adagbabiri","6,Brass","6,Ekeremor","6,Kembe","6,Kolokuma",
           //     "6,Kolokuma/Opkuma","6,Nembe","6,Ogbia","6,Sagbama","6,Southern-Ijaw","6,Toru-Abubo","6,Yenegoa","6,Provide LGA Later",
           //     "7,Ado","7,Agatu","7,Apa","7,Buruku","7,Gboko","7,Guma","7,Gwer-East","7,Gwer-West","7,Katsina-Ala","7,Konshisha",
           //     "7,Kwande","7,Logo","7,Makurdi","7,Ogbadibo","7,Ohimini","7,Oju","7,Okpokwu","7,Otukpo","7,Oturkpa","7,Tarka",
           //     "7,Ukum","7,Ushongo","7,Vandeikya","7,Provide LGA Later","8,Abadan","8,Askira-Uba","8,Bama","8,Bayo","8,Biu",
           //     "8,Chibok","8,Damboa","8,Dikwa","8,Gubio","8,Guzamala","8,Gwoza","8,Hawul","8,Jere","8,Kaga","8,Kala/Balge",
           //     "8,Konduga","8,Kukawa","8,Kwaya-Kusar","8,Mafa","8,Magumeri","8,Maiduguri","8,Marte","8,Mobbar","8,Mongunu",
           //     "8,Ngala","8,Nganzai","8,Shani","8,Provide LGA Later","9,Abi","9,Abuochichie","9,Akamkpa","9,Akpabuyo","9,Bakassi",
           //     "9,Bekwara","9,Biasi","9,Boki","9,Calabar-Municipal","9,Calabar-South","9,Etunk","9,Ikom","9,Obanliku","9,Obubra",
           //     "9,Obudu","9,Odukpani","9,Ogoja","9,Ugep-North","9,Yakurr","9,Yala","9,Provide LGA Later","10,Aniocha North",
           //     "10,Aniocha-North","10,Aniocha-South","10,Bomadi","10,Burutu","10,Effurun","10,Ethiope-East","10,Ethiope-West",
           //     "10,Idu","10,Ika-Ne","10,Ika-North-East","10,Ika-South","10,Ikpemili","10,Isoko-North","10,Isoko-South","10,Ndokwa-East",
           //     "10,Ndokwa-North","10,Ndokwa-South","10,Ndokwa-West","10,Okpe","10,Okwuani","10,Oleh","10,Oshielli-North","10,Oshimili",
           //     "10,Oshimili-North","10,Oshimili-South","10,Osimili","10,Osimili-North","10,Osimili-South","10,Patani","10,Sapele",
           //     "10,Udokwa","10,Udu","10,Ughelli-North","10,Ughelli-South","10,Ukwuani","10,Uraun","10,Urwie","10,Uvie","10,Uvwei",
           //     "10,Uvwie","10,Uwvie","10,Warri-Central","10,Warri-North","10,Warri-South","10,Provide LGA Later","11,Abakaliki",
           //     "11,Afikpo-North","11,Afikpo-South","11,Bomadim","11,Ebonyi","11,Ezza-North","11,Ezza-South","11,Ikwo","11,Ishielu",
           //     "11,Ivo","11,Izzi","11,Obaukwu","11,Ohakwu","11,Onicha","11,Ukaba","11,Provide LGA Later","12,Afokpella","12,Afuze",
           //     "12,Agbazilo","12,Akoko Edo","12,Akoko-Edo","12,Egor","12,Esan-Central","12,Esan-North-East","12,Esan-North-East",
           //     "12,Esan-South-East","12,Esan-West","12,Etsako-Central","12,Etsako-East","12,Etsako-West","12,Igueben",
           //     "12,Iguobano North East","12,Ikpoba-Okha","12,Ohunmwode","12,Ologbo","12,Opoji Irrua","12,Opoji Irrua","12,Oredo",
           //     "12,Orhionmwon","12,Ovia-North-East","12,Ovia-South-West","12,Owan East","12,Owan-East","12,Owan-West","12,Uhunmwonde",
           //     "12,Provide LGA Later","13,Ado-Ekiti","13,Aiyekire","13,Efon","13,Ekiti-East","13,Ekiti-South-West","13,Ekiti-West",
           //     "13,Emure/Ise/Orun","13,Gbonyin","13,Ido-Osi","13,Ijero","13,Ikare","13,Ikere","13,Ikole","13,Ilejemeje",
           //     "13,Irepodun/Ifelodun","13,Ise-Orun","13,Moba","13,Oye ","13,Provide LGA Later","14,Aninri","14,Awgu","14,Enugu-East",
           //     "14,Enugu-North","14,Enugu-South","14,Ezeagu","14,Igbo-Etiti","14,Igbo-Eze-North","14,Igbo-Eze-South","14,Isi-Uzo",
           //     "14,Nkanu-East","14,Nkanu-West","14,Nsukka","14,Nukanu East","14,Oji-River","14,Udenu","14,Udi","14,Uzo-Uwani",
           //     "14,Provide LGA Later","15,Abuja","15,Provide LGA Later","16,Akko","16,Balanga","16,Biddiri","16,Dukku","16,Funakaye",
           //     "16,Gombe","16,Kaltungo","16,Kwami","16,Nafada/Bajoga","16,Shomgom","16,Yamaltu/Deba","16,Provide LGA Later",
           //     "17,Aboh-Mbaise","17,Ahiazu-Mbaise","17,Dral-Esat","17,Ehime-Mbano","17,Ezeobodo","17,Ezinihitte","17,Ideato",
           //     "17,Ideato-North","17,Ideato-South","17,Ihitte/Uboma","17,Ikeduru","17,Isiala-Mbano","17,Isu","17,Mbaitoli",
           //     "17,Mbano","17,Ngor-Okpala","17,Njaba","17,Nkwerre","17,Nwangele","17,Obowo","17,Oguta","17,Ohaji-Egbema","17,Okigwe",
           //     "17,Onuimo","17,Orlu","17,Oro-West","17,Orsu","17,Oru-East","17,Oru-West","17,Owerri-Municipal","17,Owerri-North",
           //     "17,Owerri-West","17,Ugiri-Ike Ikeduru","17,Ugiri-Ikedikeduru","17,Unbano","17,Zinihitte","17,Provide LGA Later",
           //     "18,Auyo","18,Babura","18,Biriniwa","18,Birnin-Kudu","18,Bosuwa","18,Buji","18,Dutse","18,Gagarawa","18,Garki",
           //     "18,Gumel","18,Guri","18,Gwaram","18,Gwiwa","18,Hadejia","18,Jahun","18,Kafin-Hausa","18,Kaugama","18,Kazaure",
           //     "18,Kirkasamma","18,Maigatari","18,Malam-Maduri","18,Miga","18,Ringim","18,Roni","18,Sule-Tankarkar","18,Taura",
           //     "18,Yankwashi","18,Provide LGA Later","19,Birnin-Gwari","19,Chikun","19,Giwa","19,Gwagwada","19,Igabi","19,Ikara",
           //     "19,Jaba","19,Provide LGA Later","20,Ajingi","20,Albasu","20,Bagwai","20,Bebeji","20,Bichi","20,Bunkure","20,Dala",
           //     "20,Dambatta","20,Dawakin-Kudu","20,Dawakin-Tofa","20,Doguwa","20,Fagge","20,Gabasawa","20,Garko","20,Garun-Mallam",
           //     "20,Gaya","20,Gezawa","20,Gwale","20,Gwarzo","20,Kano-Municipal","20,Karaye","20,Kibiya","20,Kiru","20,Kumbotso",
           //     "20,Kunchi","20,Kura","20,Madobi","20,Makoda","20,Minjibir","20,Nasarawa","20,Rano","20,Rimin-Gado","20,Rogo",
           //     "20,Shanono","20,Sumaila","20,Takai","20,Tarauni","20,Tofa","20,Tsanyawa","20,Tudun-Wada","20,Ungogo","20,Warawa",
           //     "20,Wudil","20,Provide LGA Later","21,Bakori","21,Batagarawa","21,Batsari","21,Baure","21,Bindawa","21,Charanchi",
           //     "21,Dan-Musa","21,Dandume","21,Danji","21,Daura","21,Dutsi","21,Dutsinma","21,Faskari","21,Funtua","21,Ingawa",
           //     "21,Jibia","21,Kafur","21,Kaita","21,Kankara","21,Kankia","21,Katsina","21,Kurfi","21,Kusada","21,Mai-Adua",
           //     "21,Malumfashi","21,Mani","21,Mashi","21,Matazu","21,Musawa","21,Rimi","21,Sabuwa","21,Safana","21,Sandamu",
           //     "21,Zango","21,Provide LGA Later","22,Aleiro","22,Arewa-Dandi","22,Argungu","22,Augie","22,Bagudo","22,Birnin-Kebbi",
           //     "22,Bumza","22,Dandi","22,Danko","22,Fakai","22,Gwandu","22,Jega","22,Kalgo","22,Koko-Besse","22,Maiyama","22,Ngaski",
           //     "22,Sakaba","22,Shanga","22,Suru","22,Wasagu","22,Yauri","22,Zuru","22,Provide LGA Later","23,Adavi","23,Ajaokuta",
           //     "23,Ankpa","23,Dekina","23,Ibaji","23,Idah","23,Igalamela","23,Ijumu","23,Ikoyi-Ijumu","23,Kabba/Bunu","23,Kogi",
           //     "23,Lokoja","23,Mopa-Muro-Mopi","23,Obaji","23,Ofu","23,Ogori/Magongo","23,Okehi","23,Okene","23,Olamaboro","23,Omala",
           //     "23,Oru","23,Oyi","23,Yagba-East","23,Yagba-West","23,Provide LGA Later","24,Asa","24,Baruten","24,Edu","24,Ekiti",
           //     "24,Ilorin-East","24,Ilorin-South","24,Ilorin-West","24,Isin","24,Kaiama","24,Moro","24,Offa","24,Oke-Ero","24,Oyun",
           //     "24,Pategi","24,Provide LGA Later","25,Agege","25,Ajeromi-Ifelodun","25,Alimosho","25,Amuwo-Odofin","25,Apapa","25,Badagry",
           //     "25,Epe","25,Eti-Osa","25,Ibeju-Lekki","25,Ifako-Ijaiye","25,Ikeja","25,Ikorodu","25,Island","25,Kosofe","25,Lagos-Island",
           //     "25,Lagos-Mainland","25,Mushin","25,Ojo","25,Oshodi-Isolo","25,Shomolu","25,Somolu","25,Suru-Lere","25,Yewa-South",
           //     "25,Provide LGA Later","26,Akwanga","26,Awe","26,Doma","26,Karu","26,Keana","26,Keffi","26,Kokona","26,Lafia","26,Nassawara",
           //     "26,Nassawara Eggon","26,Obi","26,Wambu","26,Provide LGA Later","27,Agaie","27,Agwara","27,Bida","27,Borgu","27,Bosso",
           //     "27,Chanchaga","27,Edati","27,Gbako","27,Gurara","27,Katcha","27,Kontagora","27,Lapai","27,Lavun","27,Magama","27,Mariga",
           //     "27,Mashegu","27,Mokwa","27,Muya","27,Paikoro","27,Rafi","27,Rijau","27,Shiroro","27,Suleja","27,Tafa","27,Wushishi",
           //     "27,Provide LGA Later","28,Abeokuta-North","28,Abeokuta-South","28,Abor Mbaise","28,Ado-Odo/Ota","28,Adoodo-Ota","28,Ewekoro",
           //     "28,Idarapo","28,Iffo-Otta","28,Ifo","28,Igbado-North","28,Igbado-South","28,Ijebu-East","28,Ijebu-North","28,Ijebu-North-East",
           //     "28,Ijebu-Ode","28,Ikenne","28,Ilishan Remo","28,Ilugun-Alaro","28,Imeko-Afon","28,Ipokia","28,Lgayekpe","28,Obafemi-Owode",
           //     "28,Odedah","28,Odogbolu","28,Ogun-Waterside","28,Remo-North","28,Sagamu","28,Shagamu","28,Yewa","28,Provide LGA Later",
           //     "29,Akoko","29,Akoko-North","29,Akoko-North-West","29,Akoko-South","29,Akoko-South-East","29,Akure","29,Akure-North",
           //     "29,Akure-South","29,Ese-Odo","29,Idanre","29,Ifedore","29,Igbisin","29,Ikale","29,Ilaje","29,Ilaje-West","29,Ile-Oluji-Okeigbo",
           //     "29,Irele","29,Odigbo","29,Oka Ak0Ko","29,Okiti Pupa Ijuodo","29,Okiti-Pupa","29,Ondo","29,Ondo West","29,Ondo-East","29,Ose",
           //     "29,Owo","29,Provide LGA Later","30,Atakumosa","30,Atakumosa East","30,Ayeda-Ade","30,Ayedire","30,Boluwaduro","30,Boripe",
           //     "30,Ede","30,Ede North","30,Egbedore","30,Ejigbo","30,Ife","30,Ife North","30,Ife South","30,Ife-Central","30,Ife-East",
           //     "30,Ifelodun","30,Ila","30,Ilesa-East","30,Ilesa-West","30,Ilesha","30,Ilesha West","30,Irepodun","30,Irewole","30,Isokan",
           //     "30,Iwo","30,Obokun","30,Odo-Otin","30,Ola Oluwa","30,Olorunda","30,Ori-Ade","30,Orolu","30,Osogbo","30,Provide LGA Later",
           //     "31,Afijio","31,Akinyele","31,Atiba","31,Atigbo","31,Egbeda","31,Ibadan-Central","31,Ibadan-North-East","31,Ibadan-North-West",
           //     "31,Ibadan-South-East","31,Ibadan-South-West","31,Ibarapa-Central","31,Ibarapa-East","31,Ibarapa-North","31,Ido","31,Ifedayo",
           //     "31,Ifeloju","31,Irepo","31,Iseyin","31,Itseiwaju","31,Iwajowa","31,Kajola","31,Lagelu","31,Odo-Oluwa","31,Ogbomosho-North",
           //     "31,Ogbomosho-South","31,Olorunsogo","31,Oluyole","31,Ona-Ara","31,Orelope","31,Ori-Ire","31,Oyo-West","31,Saki-East",
           //     "31,Saki-West","31,Surulere","31,Provide LGA Later","32,Barkin-Ladi","32,Bassa","32,Bokkos","32,Jos-East","32,Jos-North",
           //     "32,Jos-South","32,Kanam","32,Kanke","32,Langtang-North","32,Langtang-South","32,Mangu","32,Mikang","32,Pankshin",
           //     "32,Provide LGA Later","33,Aboa/Odual","33,Ahoada-East","33,Ahoada-West","33,Akukutoru","33,Andoni","33,Asari-Toru",
           //     "33,Bonny","33,Buguma","33,Degema","33,Eleme","33,Elfane","33,Emuoha","33,Etche","33,Gokana","33,Ikwerre","33,Khana",
           //     "33,Obia/Akpor","33,Ogba-Egbema-Ndoni","33,Ogba/Egbema/Ndoni","33,Ogu/Bolo","33,Okirika","33,Omuma","33,Opobo/Nkoro",
           //     "33,Oyigbo","33,Port-Harcourt","33,Tai","33,Provide LGA Later","34,Binji","34,Bodinga","34,Dange-Shuni","34,Gada","34,Goronyo",
           //     "34,Gudu","34,Gwadabawa","34,Illela","34,Kebbe","34,Kware","34,Raba","34,Sabo-Birni","34,Shagari","34,Silame","34,Sokoto-North",
           //     "34,Sokoto-South","34,Tambuwal","34,Tangaza","34,Tureta","34,Wamakko","34,Wurno","34,Yabo","34,Provide LGA Later","35,Ardo-Kola",
           //     "35,Bali","35,Donga","35,Gashaka","35,Gassol","35,Ibi","35,Jalingo","35,Karim-Lamido","35,Kurmi","35,Lau","35,Oilingo",
           //     "35,Sardauna","35,Takum","35,Ussa","35,Wukari","35,Yorro","35,Zing","35,Provide LGA Later","36,Bade","36,Borsali","36,Damaturu",
           //     "36,Fika","36,Fune","36,Geidam","36,Gogaram","36,Gujba","36,Gulani","36,Jakusko","36,Karasuwa","36,Machina","36,Nangere",
           //     "36,Nguru","36,Potiskum","36,Tarmua","36,Yunusari","36,Yusufari","36,Provide LGA Later","37,Anka","37,Bakura","37,Bukkuyum",
           //     "37,Bungudu","37,Gumi","37,Gusau","37,Isa","37,Kaura-Namoda","37,Kiyawa","37,Maradun","37,Maru","37,Shinkafi","37,Talata-Mafara",
           //     "37,Tsafe","37,Zurmi","37,Provide LGA Later","38,Provide LGA Later"// Add the rest of your LGAs as required
           // };
           // foreach (var lga in lgas)
           // {
           //     var lgaParts = lga.Split(',');
           //     var stateId = byte.Parse(lgaParts[0]);
           //     var name = lgaParts[1];
           //     if (!await db.LocalGovernments.AnyAsync(l => l.Name == name))
           //     {
           //         db.LocalGovernments.Add(new LocalGovernment { StateId = stateId, Name = name });
           //     }
           // }
           // await db.SaveChangesAsync();

           // // Affective & Psychomotor
           // var affectiveAndPsychomotor = new List<string>
           // {
           //     "1,Attentiveness",
           //     "2,Honesty",
           //     "3,Neatness",
           //     "4,Politeness",
           //     "5,Punctuality",
           //     "6,Relationship with Others",
           //     "7,Club/Society",
           //     "8,Drawing and Painting",
           //     "9,Hand Writing",
           //     "10,Hobbies",
           //     "11,Speech Fluency",
           //     "12,Sport and Game"
           // };
           // foreach (var item in affectiveAndPsychomotor)
           // {
           //     var parts = item.Split(',');
           //     var id = byte.Parse(parts[0]);
           //     var itemName = parts[1];
           //     if (!await db.AffectiveAndPsychomotors.AnyAsync(l => l.ItemsName == itemName))
           //     {
           //         db.AffectiveAndPsychomotors.Add(new AffectiveXzAndPsychomotor { Id = id, ItemsName = itemName });
           //     }
           // }
           // await db.SaveChangesAsync();

           // // License/Program Info
           // var licenseDefaultData = new List<string>
           // {
           //     "1,Virtual,",
           //     "2,Virtual2,",
           //     "3,Virtual3,",
           //     "4,Virtual4,",
           //     "5,Virtual5,",
           //     "6,Virtual6,",
           //     "7,Virtual7,",
           //     "9,ComputerID,",
           //     "10," + DateTimeToJsonAndEncrypt(new DateTime(2021, 1, 1, 0, 0, 0)) + ",",
           //     "11,SerialNo,",
           //     "12,Start_Time," + DateTimeToJsonAndEncrypt(new DateTime(2017, 1, 1, 0, 0, 0)),
           //     "13,End_Time,04/04/2021 12:00:00 AM",
           //     "14,Reg_Status,2",
           //     "15,Status,85pkGugg8Jw=",
           //     "16,Days_Remaining,0",
           //     "17,Attempt," + DateTimeToJsonAndEncrypt(new DateTime(2017, 1, 1, 0, 0, 0)),
           //     "18,Value,nHqCgkfRftw"
           // };
           // foreach (var item in licenseDefaultData)
           // {
           //     var itemToList = item.Split(',').ToList();
           //     var id = byte.Parse(itemToList[0]);
           //     var machines = itemToList[1];
           //     var matches = itemToList[2];
           //     if (!await db.ProgramInfos.AnyAsync(l => l.Id == id))
           //     {
           //         db.ProgramInfos.Add(new ProgramYInfo() { Id = id, Machines = machines, Matches = matches });
           //     }
           // }
           // await db.SaveChangesAsync();

           // // School head categories
           // var schoolHeadCategoy = new List<string>
           //{
           //     "1,0",
           //     "2,1",
           //};
           // foreach (var item in schoolHeadCategoy)
           // {
           //     var parts = item.Split(',');
           //     var id = byte.Parse(parts[0]);
           //     var categoryName = int.Parse(parts[1]);
           //     if (!await db.SchoolHeadCategories.AnyAsync(l => l.Id == id))
           //     {
           //         db.SchoolHeadCategories.Add(new SchoolHeadTxCategory()
           //         { Id = id, HeadStatus = (HeadStatus)categoryName });
           //     }
           // }
           // await db.SaveChangesAsync();

           // // Academic terms
           // var academicTerm = new List<string>
           // {
           //     "1,0",
           //     "2,1",
           //     "3,2"
           // };
           // foreach (var item in academicTerm)
           // {
           //     var parts = item.Split(',');
           //     var id = byte.Parse(parts[0]);
           //     var termName = byte.Parse(parts[1]);
           //     if (!await db.Terms.AnyAsync(l => l.Id == id))
           //     {
           //         db.Terms.Add(new Term()
           //         { Id = id, TermStatus = (TermStatus)termName });
           //     }
           // }
           // await db.SaveChangesAsync();

           // // Roles
           // var roles = new[]
           // {
           //     "Accounting","Registry","Teacher","CbtTeacher","StudentOrParent","LocalAgents","RemoteAgents","SAOfficer"
           //};
           // foreach (var role in roles)
           // {
           //     if (!await roleManager.RoleExistsAsync(role))
           //         await roleManager.CreateAsync(new IdentityRole { Name = role, NormalizedName = role.ToUpper() });
           // }

           // // Users
           // async Task<ApplicationUser> EnsureUserAsync(string userName, string email,
           //     string first, string last, Gender gender, string password, UserStatus userStatus, string? id = null)
           // {
           //     var user = await userManager.FindByNameAsync(userName);
           //     if (user != null) return user;

           //     var u = new ApplicationUser
           //     {
           //         Id = id ?? Guid.NewGuid().ToString(),
           //         UserName = userName,
           //         NormalizedUserName = userName.ToUpperInvariant(),
           //         Email = email,
           //         NormalizedEmail = email.ToUpperInvariant(),
           //         EmailConfirmed = true,
           //         FirstName = first,
           //         MiddleName = "",
           //         SurName = last,
           //         Gender = gender,
           //         SecurityStamp = Guid.NewGuid().ToString(),
           //         CreatedDate = DateTime.UtcNow,
           //         UserStatus = userStatus,
           //     };

           //     var result = await userManager.CreateAsync(u, password);
           //     if (!result.Succeeded)
           //     {
           //         var msg = string.Join("; ", result.Errors.Select(e => e.Description));
           //         throw new InvalidOperationException($"Failed to create user {userName}: {msg}");
           //     }
           //     return u;
           // }
           // var systemAdminUserId = Guid.NewGuid().ToString();
           // var schoolAdminUserId = Guid.NewGuid().ToString();
           // var agent = await EnsureUserAsync("agentofficer", "agent@naits.com", "Agent", "User", Gender.Male, "Agent@123456", UserStatus.Agent);
           // var cbt = await EnsureUserAsync("cbtTeacher", "cbtTeacher@naits.com", "CbtTeacher", "User", Gender.Male, "CbtTeacher@123456", UserStatus.CbtTeacher);
           // var supperAdmin = await EnsureUserAsync("saofficer", "sao@naits.com", "SupperAdmin", "User", Gender.Male, "SAOP@bc123456", UserStatus.SystemAdmin, systemAdminUserId);
           // var registry = await EnsureUserAsync("schooladmin", "sadmin@naits.com", "SchoolAdmin", "User", Gender.Male, "SAOP@bc123456", UserStatus.SchoolAdmin, schoolAdminUserId);

           // async Task EnsureInRoleAsync(ApplicationUser u, string role)
           // {
           //     if (!await userManager.IsInRoleAsync(u, role))
           //         await userManager.AddToRoleAsync(u, role);
           // }

           // await EnsureInRoleAsync(cbt, "CbtTeacher");
           // await EnsureInRoleAsync(agent, "LocalAgents");
           // await EnsureInRoleAsync(supperAdmin, "SAOfficer");
           // await EnsureInRoleAsync(registry, "Registry");


           // var adminAsAgent = new Agent { UserId = supperAdmin.Id };
           // var localAgent = new Agent { UserId = agent.Id };
           // db.Add(adminAsAgent);
           // db.Add(localAgent);

           // var schoolAdminOfficer = new SchoolRegistrationOfficer
           // {
           //     UserId = schoolAdminUserId,
           //     DateOfBirth = DateTime.UtcNow,
           //     FullAddress = string.Empty,
           //     SignatureImageString = string.Empty,
           //     TimeStatus = TimeStatus.Current,
           //     AccessStatus = AccessStatus.Active,
           //     AgentUserId = systemAdminUserId
           // };
           // db.Add(schoolAdminOfficer);
           // var schoolModel = new SchoolTyInfo
           // {
           //     SchoolRegistrationOfficerId = schoolAdminUserId,
           //     SchoolName = "New Age College"
           // };
           // db.Add(schoolModel);
           // await db.SaveChangesAsync();
        }

        private static string DateTimeToJsonAndEncrypt(DateTime date)
        {
            var dateToJsonVal = JsonConvert.SerializeObject(date);
            return FreePlugIn.Encrypt(dateToJsonVal, true);

        }
    }
}