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

    public virtual DbSet<OrdersTb> OrdersTbs { get; set; }

    public virtual DbSet<ProductsTb> ProductsTbs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=DESKTOP-SV4QTVS;Initial Catalog=NocturnalBrews;Persist Security Info=True;TrustServerCertificate=True; User ID=sa;Password=Shuku@ki04042001");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrdersTb>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__OrdersTb__C3905BCF9C13DDB1");

            entity.ToTable("OrdersTb");

            entity.Property(e => e.Mop)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OrderDateTime).HasColumnType("datetime");
            entity.Property(e => e.Total).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<ProductsTb>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__Products__B40CC6CDB0F9B267");

            entity.ToTable("ProductsTb");

            entity.Property(e => e.Price)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProductName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Size)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
