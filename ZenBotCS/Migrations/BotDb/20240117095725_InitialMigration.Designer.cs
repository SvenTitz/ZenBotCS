﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ZenBotCS.Entities;

#nullable disable

namespace ZenBotCS.Migrations.BotDb
{
    [DbContext(typeof(BotDataContext))]
    [Migration("20240117095725_InitialMigration")]
    partial class InitialMigration
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("ZenBotCS.Entities.Models.DiscordLink", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<ulong>("DiscordId")
                        .HasColumnType("bigint unsigned");

                    b.Property<string>("PlayerTag")
                        .IsRequired()
                        .HasMaxLength(12)
                        .HasColumnType("varchar(12)");

                    b.Property<DateTime>("UpdatedAt")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime(6)");

                    b.HasKey("Id");

                    b.HasIndex("PlayerTag")
                        .IsUnique();

                    b.HasIndex("PlayerTag", "DiscordId")
                        .IsUnique();

                    b.ToTable("DiscordLinks");
                });
#pragma warning restore 612, 618
        }
    }
}
