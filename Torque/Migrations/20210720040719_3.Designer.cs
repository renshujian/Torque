﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Oracle.EntityFrameworkCore.Metadata;
using Torque;

namespace Torque.Migrations
{
    [DbContext(typeof(MesDbContext))]
    [Migration("20210720040719_3")]
    partial class _3
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("ProductVersion", "5.0.8")
                .HasAnnotation("Oracle:ValueGenerationStrategy", OracleValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("Torque.Test", b =>
                {
                    b.Property<DateTime>("TestTime")
                        .HasColumnType("TIMESTAMP(7)")
                        .HasColumnName("test_time");

                    b.Property<string>("Diviation")
                        .IsRequired()
                        .HasColumnType("NVARCHAR2(64)")
                        .HasColumnName("diviation");

                    b.Property<string>("RealTorque")
                        .IsRequired()
                        .HasColumnType("NVARCHAR2(64)")
                        .HasColumnName("real_torque");

                    b.Property<string>("SetTorque")
                        .IsRequired()
                        .HasColumnType("NVARCHAR2(64)")
                        .HasColumnName("set_torque");

                    b.Property<string>("ToolId")
                        .IsRequired()
                        .HasColumnType("NVARCHAR2(2000)")
                        .HasColumnName("screwdriver");

                    b.HasKey("TestTime");

                    b.ToTable("real_torque_of_screwdriver");
                });

            modelBuilder.Entity("Torque.Tool", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("NVARCHAR2(450)")
                        .HasColumnName("screwdriver");

                    b.Property<string>("SetTorque")
                        .IsRequired()
                        .HasColumnType("NVARCHAR2(64)")
                        .HasColumnName("XYNJ");

                    b.HasKey("Id");

                    b.ToTable("screwdriver_CMK");

                    b.HasData(
                        new
                        {
                            Id = "50mppmu1N0vovmnmmmmmqnnpmtmnmj1E0toml1E0gmmm",
                            SetTorque = "50"
                        },
                        new
                        {
                            Id = "90ehhem1G0nemefeeeeffmfhelefebxgmnYeee",
                            SetTorque = "9"
                        },
                        new
                        {
                            Id = "03308K9290100000411307010-B720/B*000",
                            SetTorque = "720.8"
                        },
                        new
                        {
                            Id = "03308L9080100001181307010-C289*000",
                            SetTorque = "289"
                        },
                        new
                        {
                            Id = "720289",
                            SetTorque = "0.13"
                        });
                });
#pragma warning restore 612, 618
        }
    }
}