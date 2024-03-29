﻿using DvD_Api.Data;
using DvD_Api.DTO;
using DvD_Api.Extentions;
using DvD_Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using System.Text;

namespace DvD_Api.Controllers
{

    [ApiController]
    [Route("api/[Controller]")]
    [Authorize]
    public class MemberController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public MemberController(ApplicationDbContext database)
        {
            _db = database;
        }

        [HttpPost]
        public async Task<IActionResult> CreateMember(AddMemberDto member)
        {

            if(member.DateOfBirth.AddYears(13) > DateTime.Now){
                return UnprocessableEntity("You need to be 13 or older to be a member!");
            }

            using var transaction = _db.Database.BeginTransaction();
            // Add both member and category or none.
            try
            {
                var membershipCategory = member.MembershipCategory;
                if (membershipCategory.McategoryNumber == 0)
                {
                    // Add the category first and get a new id.
                    await _db.MembershipCategories.AddAsync(membershipCategory);
                    await _db.SaveChangesAsync();
                }

                if (!string.IsNullOrWhiteSpace(member.ProfileImage))
                {
                    try
                    {

                        SKBitmap imageBitmap = member.ProfileImage.GetBitmap();
                        SKPixmap pixMap = imageBitmap.PeekPixels();

                        var options = new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, 50);
                        SKData data = pixMap.Encode(options); 

                        var base64String = "data:image/webp;base64," + data.AsStream().ConvertToBase64();
                        member.ProfileImage = base64String;

                    }
                    catch (Exception)
                    {

                        Console.WriteLine("Not a valid base64 ");
                    }
                }

                var nMember = new Member
                {
                    MemberNumber = 0,
                    CategoryNumber = membershipCategory.McategoryNumber,
                    FirstName = member.FristName,
                    LastName = member.LastName,
                    Address = member.Address,
                    DateOfBirth = member.DateOfBirth,
                    ProfileImage64 = member.ProfileImage,
                };

                await _db.Members.AddAsync(nMember);

                await _db.SaveChangesAsync();
                transaction.Commit();

                return Ok($"Added new member with id {nMember.MemberNumber}");
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Could not add member. Contact Admin!");
            }

        }

        [HttpGet]
        public IEnumerable<Member> GetAllMembers()
        {
            return _db.Members.Include(m => m.CategoryNumberNavigation);
        }

        [HttpGet("{memberId}")]
        public object GetMember(int memberId)
        {
            return _db.Members
                .Include(m => m.CategoryNumberNavigation)
                .Include(m => m.Loans)
                .ThenInclude(l => l.CopyNumberNavigation)
                .ThenInclude(c => c.DvdnumberNavigation)
                .Where(m => m.MemberNumber == memberId)
                .Select(m => new {
                    MemberName = $"{m.FirstName} {m.LastName}",
                    Loans = m.Loans.Where(l => l.DateOut.AddDays(31) >= DateTime.Now).Select(l => new {
                        LoanId = l.LoanNumber,
                        DvdTitle = l.CopyNumberNavigation.DvdnumberNavigation.DvdName,
                        CopyId = l.CopyNumber,
                        DateOut = l.DateOut,
                        DateDue = l.DateDue,
                        ReturnedDate = l.DateReturned.Value.ToString("d") ?? "Not Returned"
                    })
                });
        }

        [HttpGet("search/{lastName}")]
        public object GetMemberByLastName(string lastName)
        {

            return _db.Members
                .Include(m => m.CategoryNumberNavigation)
                .Include(m => m.Loans)
                .ThenInclude(l => l.CopyNumberNavigation)
                .ThenInclude(c => c.DvdnumberNavigation)
                .Where(m => m.LastName == lastName)
                .Select(m => new { 
                    MemberName = $"{m.FirstName} {m.LastName}",
                    Loans = m.Loans.Where(l => l.DateOut.AddDays(31) >= DateTime.Now ).Select(l => new { 
                        LoanId = l.LoanNumber,
                        DvdTitle = l.CopyNumberNavigation.DvdnumberNavigation.DvdName,
                        CopyId = l.CopyNumber,
                        DateOut = l.DateOut, 
                        DateDue = l.DateDue, 
                        ReturnedDate = l.DateReturned.Value.ToString("d") ?? "Not Returned"
                    })
                });
        }

        [HttpGet("forLoan")]
        public IEnumerable<object> GetMemberForLoan()
        {
            return _db.Members.Select(m => new
            {
                MemberId = m.MemberNumber,
                MemberName = $"{m.FirstName} {m.LastName}",
                IsOfAge = m.IsOldEnough()
            });
        }

        [HttpPut]
        public async Task<IActionResult> UpdateMember(int memberId, Member member)
        {
            if (memberId != member.MemberNumber)
            {
                return BadRequest();
            }
            var memberExists = _db.Members.Where(m => m.MemberNumber == memberId).Any();
            if (memberExists)
            {
                _db.Members.Update(member);
                await _db.SaveChangesAsync();

                return Ok();

            }

            return NotFound($"Member with id {memberId} not found!");
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteMember(int memberId)
        {
            var memberExists = await _db.Members.Where(m => m.MemberNumber == memberId).FirstOrDefaultAsync();
            if (memberExists == null)
            {
                return NotFound($"Member with id {memberId} not found!");
            }

            _db.Members.Remove(memberExists);
            await _db.SaveChangesAsync();

            return Ok();

        }

        [HttpGet("memberWithLoans")]
        public IEnumerable<object> GetMemberWithLoans()
        {
            return _db.Members
                .Include(m => m.CategoryNumberNavigation)
                .Include(m => m.Loans)
                .OrderBy(m => m.FirstName)
                .Select(m => new
                {
                    MemberId = m.MemberNumber,
                    FirstName = m.FirstName,
                    LastName = m.LastName,
                    MembershipCategory = m.CategoryNumberNavigation.Description,
                    DateOfBirth = m.DateOfBirth.ToString("d"),
                    LimitStatus = m.Loans.Where(l => l.DateReturned == null).Count() > m.CategoryNumberNavigation.TotalLoans ? "Limit Crossed" : "Ok",
                    TotalLoans = m.Loans.Count,
                    CurrentLoanCount = m.Loans.Where(l => l.DateReturned == null).Count()
                }).ToList().GroupBy(m => m.FirstName.ToLower()[0])
                .Select(d => new
                {
                    Alphabet = d.Key,
                    MemberList = d
                });

        }


        [HttpGet("nonActive")]
        public IEnumerable<object> GetInactiveMembers()
        {
            return _db.Members
                .Include(m => m.Loans)
                .ThenInclude(l => l.CopyNumberNavigation)
                .ThenInclude(c => c.DvdnumberNavigation)
                .Where(m => m.Loans.OrderBy(l => l.DateOut)
                .LastOrDefault().DateOut.AddDays(31) < DateTime.Now)
                .OrderBy(m => m.Loans.OrderBy(l => l.DateOut).LastOrDefault().DateOut).Select(o => new { 
                    FirstName = o.FirstName,
                    LastName = o.LastName,
                    Address = o.Address,
                    RecentDvdTitle = o.Loans.OrderBy(l => l.DateOut).LastOrDefault().CopyNumberNavigation.DvdnumberNavigation.DvdName,
                    DaysSinceLoan = (DateTime.Now - o.Loans.OrderBy(l => l.DateOut).LastOrDefault().DateOut).Days,
                    DateOut = o.Loans.OrderBy(l => l.DateOut).LastOrDefault().DateOut.ToString("d"),
                    MemberImage = o.ProfileImage64
                });
        }


    }
}
