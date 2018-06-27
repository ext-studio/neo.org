﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Neo;
using Neo.IO.Json;
using NeoWeb.Data;
using NeoWeb.Models;

namespace NeoWeb.Controllers
{
    public class CandidateController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<CandidateController> _localizer;

        public CandidateController(ApplicationDbContext context, IStringLocalizer<CandidateController> localizer)
        {
            _context = context;
            _localizer = localizer;
        }

        static Timer timer = new Timer();
        static List<CandidateViewModels> candidateList = new List<CandidateViewModels>();

        // GET: Candidate
        public async Task<IActionResult> Index()
        {
            timer.Elapsed += Timer_Elapsed;
            timer.Interval = new TimeSpan(0, 1, 0).TotalMilliseconds;
            timer.Start();
            return View(await _context.Candidates.ToListAsync());
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var response = Helper.PostWebRequest("http://localhost:10332", "{'jsonrpc': '2.0', 'method': 'getvalidators', 'params': [],  'id': 1}");
            var json = JObject.Parse(response)["result"];
            JArray list = (JArray)json;
            var result = new List<CandidateViewModels>();
            foreach (var item in list)
            {
                var c = CandidateViewModels.FromJson(item);
                c.Info = _context.Candidates.FirstOrDefault(p => p.PublicKey == c.PublicKey);
                c.State = c.Info == null || c.Info.IP == null ? NodeState.Unknown : IsConnect(c.Info.IP) ? NodeState.Online : NodeState.Offline;
                result.Add(c);
            }
            candidateList = result;
        }

        [HttpGet]
        public JsonResult Getvalidators()
        {
            return Json(candidateList);
        }

        private bool IsConnect(string ip)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                SendTimeout = 1000,
                ReceiveTimeout = 1000
            };
            try
            {
                socket.Connect(ip, 10333);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // GET: Candidate/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var Candidate = await _context.Candidates
                .FirstOrDefaultAsync(m => m.PublicKey == id);
            if (Candidate == null)
            {
                return NotFound();
            }

            return View(Candidate);
        }

        // GET: Candidate/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Candidate/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string signature, [Bind("PublicKey,Email,IP,Website,Details,Location,SocialAccount,Telegram,Summary,")] Candidate c)
        {
            if (ModelState.IsValid)
            {
                //VerifySignature
                var message = "candidate" + c.Email + c.Details;
                if (!VerifySignature(message, signature, c.PublicKey))
                {
                    ViewBag.Message = _localizer["Signature Verification Failure"];
                    return View(c);
                }
                //Insert or Update
                var item = _context.Candidates.FirstOrDefault(p => p.PublicKey == c.PublicKey);
                if (item == null) 
                {
                    _context.Add(c);
                }
                else
                {
                    item = c;
                    _context.Update(item);
                }
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(c);
        }
        
        private bool VerifySignature(string message, string signature, string pubkey)
        {
            var msg = System.Text.Encoding.Default.GetBytes(message);
            try
            {
                return VerifySignature(msg, signature.HexToBytes(), pubkey.HexToBytes());
            }
            catch (Exception)
            {
                return false;
            }
        }

        //reference https://github.com/neo-project/neo/blob/master/neo/Cryptography/Crypto.cs
        private bool VerifySignature(byte[] message, byte[] signature, byte[] pubkey)
        {
            if (pubkey.Length == 33 && (pubkey[0] == 0x02 || pubkey[0] == 0x03))
            {
                try
                {
                    pubkey = Neo.Cryptography.ECC.ECPoint.DecodePoint(pubkey, Neo.Cryptography.ECC.ECCurve.Secp256r1).EncodePoint(false).Skip(1).ToArray();
                }
                catch
                {
                    return false;
                }
            }
            else if (pubkey.Length == 65 && pubkey[0] == 0x04)
            {
                pubkey = pubkey.Skip(1).ToArray();
            }
            else if (pubkey.Length != 64)
            {
                throw new ArgumentException();
            }
            using (var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
                {
                    X = pubkey.Take(32).ToArray(),
                    Y = pubkey.Skip(32).ToArray()
                }
            }))
            {
                return ecdsa.VerifyData(message, signature, HashAlgorithmName.SHA256);
            }
        }
    }
}
