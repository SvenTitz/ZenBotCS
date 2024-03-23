﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ZenBotCS.Entities;

#nullable disable

namespace ZenBotCS.Migrations.BotDb
{
    [DbContext(typeof(BotDataContext))]
    [Migration("20240317102328_AddCwlSignup")]
    partial class AddCwlSignup
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            MySqlModelBuilderExtensions.AutoIncrementColumns(modelBuilder);

            modelBuilder.Entity("ZenBotCS.Entities.Models.CwlSignup", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("Id"));

                    b.Property<bool>("Archieved")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("Bonus")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("ClanTag")
                        .IsRequired()
                        .HasMaxLength(12)
                        .HasColumnType("varchar(12)");

                    b.Property<ulong>("DiscordId")
                        .HasColumnType("bigint unsigned");

                    b.Property<int>("OptOutDays")
                        .HasColumnType("int");

                    b.Property<string>("PlayerName")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("PlayerTag")
                        .IsRequired()
                        .HasMaxLength(12)
                        .HasColumnType("varchar(12)");

                    b.Property<int>("PlayerThLevel")
                        .HasColumnType("int");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("datetime(6)");

                    b.Property<bool>("WarGeneral")
                        .HasColumnType("tinyint(1)");

                    b.Property<int>("WarPreference")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("CwlSignups");
                });

            modelBuilder.Entity("ZenBotCS.Entities.Models.DiscordLink", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("Id"));

                    b.Property<ulong>("DiscordId")
                        .HasColumnType("bigint unsigned");

                    b.Property<string>("PlayerTag")
                        .IsRequired()
                        .HasMaxLength(12)
                        .HasColumnType("varchar(12)");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("datetime(6)");

                    b.HasKey("Id");

                    b.HasIndex("PlayerTag")
                        .IsUnique();

                    b.HasIndex("PlayerTag", "DiscordId")
                        .IsUnique();

                    b.ToTable("DiscordLinks");
                });

            modelBuilder.Entity("ZenBotCS.Entities.Models.WarHistory", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ClanTag")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("varchar(50)");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("WarData")
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.ToTable("WarHistories");
                });
#pragma warning restore 612, 618
        }
    }
}
