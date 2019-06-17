using SampleApp.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Guid newMemberId = Guid.NewGuid();

            //New Entity
            using (MemberRepository memberRepository = new MemberRepository())
            {
                var member = memberRepository.CreateNew();
                member.Id = newMemberId;
                member.Title = "Test Member";
                member.UserName = "username";
                member.Password = "password";
                member.Email = "test@test.com";
                memberRepository.Save();
            }

            //Update Multiple Entity
            using (MemberRepository memberRepository = new MemberRepository())
            {
                var members = memberRepository.GetAll(query => query.Timestamp >= DateTime.Today).ToList();

                foreach (var member in members)
                {
                    member.Title += " Lastname";
                }

                memberRepository.Save();
            }

            //Update Single Entity
            using (MemberRepository memberRepository = new MemberRepository())
            {
                var member = memberRepository.GetSingle(query => query.Id == newMemberId);

                if (member != null)
                {
                    member.Title = "Selected Member 2";
                    memberRepository.Save();
                }
            }

            //IsNew
            using (MemberRepository memberRepository = new MemberRepository())
            {
                var member = memberRepository.CreateNew();
                Console.WriteLine("Is my entity new? Answer: " + memberRepository.IsNew(member));
            }

            //Delete Entity
            using (MemberRepository memberRepository = new MemberRepository())
            {
                memberRepository.Delete(query => query.Title == "Selected Member 2");
                memberRepository.Save();
            }
        }
    }
}
