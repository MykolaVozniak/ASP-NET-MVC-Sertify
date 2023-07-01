﻿using AutoMapper;
using Certify.Models;

namespace Certify.ViewModels.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            this.CreateMap<Document, DocumentViewModel>()
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.Firstname + " " + src.User.Lastname));

            this.CreateMap<User, DocumentViewModel>()
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Firstname + " " + src.Lastname));

            CreateMap<DocumentViewModel, Document>();
            this.CreateMap<Document, DocumentIndex>()
               .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
               .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Title))
               .ForMember(dest => dest.FileURL, opt => opt.MapFrom(src => src.FileURL));


            this.CreateMap<Document, DocumentEdit>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Title))
                .ForMember(dest => dest.ShortDescription, opt => opt.MapFrom(src => src.ShortDescription));

        }
    }
}
