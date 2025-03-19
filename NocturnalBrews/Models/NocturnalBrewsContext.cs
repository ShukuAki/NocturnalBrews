using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace NocturnalBrews.Models;

public partial class NocturnalBrewsContext : DbContext
{
    public NocturnalBrewsContext()
    {
    }

    public NocturnalBrewsContext(DbContextOptions<NocturnalBrewsContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Addon> Addons { get; set; }

    public virtual DbSet<CupsListTb> CupsListTbs { get; set; }

    public virtual DbSet<InventoryTb> InventoryTbs { get; set; }

    public virtual DbSet<OrdersTb> OrdersTbs { get; set; }

    public virtual DbSet<ProductsTb> ProductsTbs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=DESKTOP-SV4QTVS;Initial Catalog=NocturnalBrews;Persist Security Info=True;TrustServerCertificate=True; User ID=sa;Password=Shuku@ki04042001");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Addon>(entity =>
        {
            entity.HasKey(e => e.AddonId).HasName("PK__Addons__742895338D0391F8");

            entity.Property(e => e.AddonName)
                .HasMaxLength(30)
                .IsUnicode(false);
        });

        modelBuilder.Entity<CupsListTb>(entity =>
        {
            entity.HasKey(e => e.CupId).HasName("PK__CupsList__2C2806B499BAD368");

            entity.ToTable("CupsListTb");

            entity.Property(e => e.Size)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<InventoryTb>(entity =>
        {
            entity.HasKey(e => e.InvId).HasName("PK__Inventor__9DC82C6A1C1264F4");

            entity.ToTable("InventoryTb");

            entity.Property(e => e.Ingredient)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Measurement)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("measurement");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.Remaining)
                .HasColumnType("decimal(18, 0)")
                .HasColumnName("remaining");
            entity.Property(e => e.Timestamp)
                .HasColumnType("datetime")
                .HasColumnName("timestamp");
        });

        modelBuilder.Entity<OrdersTb>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__OrdersTb__C3905BCF9C13DDB1");

            entity.ToTable("OrdersTb");

            entity.Property(e => e.Mop)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OrderDateTime).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.Total).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<ProductsTb>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__Products__B40CC6CDB0F9B267");

            entity.ToTable("ProductsTb");

            entity.Property(e => e.Categories).HasMaxLength(100);
            entity.Property(e => e.ProductName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
