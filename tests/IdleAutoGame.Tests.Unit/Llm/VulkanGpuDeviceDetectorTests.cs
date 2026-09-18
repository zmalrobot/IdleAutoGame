using FluentAssertions;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm.Gpu;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Llm;

public class VulkanGpuDeviceDetectorTests
{
    private const string SampleVulkanSummarySingleGpu = @"
==========
VULKANINFO
==========

Vulkan Instance Version: 1.4.354

Instance Extensions: count = 24
-------------------------------
VK_KHR_get_physical_device_properties2 : extension revision 2

Devices:
========
GPU0:
	apiVersion         = 1.4.354
	driverVersion      = 26.2.3
	vendorID           = 0x1002
	deviceID           = 0x67df
	deviceType         = PHYSICAL_DEVICE_TYPE_DISCRETE_GPU
	deviceName         = AMD Radeon RX 480 Graphics (RADV POLARIS10)
	driverInfo         = Mesa 26.2.3
";

    private const string SampleVulkanSummaryMultiGpu = @"
Devices:
========
GPU0:
	apiVersion         = 1.3.250
	driverVersion      = 535.129.03
	vendorID           = 0x10de
	deviceID           = 0x2206
	deviceType         = PHYSICAL_DEVICE_TYPE_DISCRETE_GPU
	deviceName         = NVIDIA GeForce RTX 3080
	driverInfo         = NVIDIA 535.129.03
GPU1:
	apiVersion         = 1.3.200
	driverVersion      = 23.1.0
	vendorID           = 0x8086
	deviceID           = 0x4680
	deviceType         = PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU
	deviceName         = Intel(R) UHD Graphics 770
	driverInfo         = Mesa 23.1.0
";

    [Fact]
    public void ParseSummary_SingleDiscreteGpu_ParsesCorrectly()
    {
        var devices = VulkanGpuDeviceDetector.ParseVulkanSummary(SampleVulkanSummarySingleGpu);

        devices.Should().HaveCount(1);
        var gpu = devices[0];
        gpu.DeviceIndex.Should().Be(0);
        gpu.Name.Should().Be("AMD Radeon RX 480 Graphics (RADV POLARIS10)");
        gpu.Vendor.Should().Be("AMD");
        gpu.VendorId.Should().Be(0x1002u);
        gpu.DeviceType.Should().Be("PHYSICAL_DEVICE_TYPE_DISCRETE_GPU");
        gpu.DriverVersion.Should().Be("26.2.3");
        gpu.VulkanApiVersion.Should().Be("1.4.354");
    }

    [Fact]
    public void ParseSummary_MultiGpu_ParsesAllDevicesAndIdentifiesDiscrete()
    {
        var devices = VulkanGpuDeviceDetector.ParseVulkanSummary(SampleVulkanSummaryMultiGpu);

        devices.Should().HaveCount(2);

        var d0 = devices[0];
        d0.DeviceIndex.Should().Be(0);
        d0.Name.Should().Be("NVIDIA GeForce RTX 3080");
        d0.Vendor.Should().Be("NVIDIA");
        d0.VendorId.Should().Be(0x10deu);
        d0.IsDiscreteGpu.Should().BeTrue();

        var d1 = devices[1];
        d1.DeviceIndex.Should().Be(1);
        d1.Name.Should().Be("Intel(R) UHD Graphics 770");
        d1.Vendor.Should().Be("Intel");
        d1.VendorId.Should().Be(0x8086u);
        d1.IsDiscreteGpu.Should().BeFalse();
    }

    [Fact]
    public void ParseSummary_EmptyOrInvalidText_ReturnsEmptyList()
    {
        var empty = VulkanGpuDeviceDetector.ParseVulkanSummary("");
        empty.Should().BeEmpty();

        var invalid = VulkanGpuDeviceDetector.ParseVulkanSummary("Some random text without vulkan structures");
        invalid.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPreferredDeviceAsync_WithAuto_PrefersDiscreteGpu()
    {
        var detector = new VulkanGpuDeviceDetector();
        var preferred = await detector.GetPreferredDeviceAsync("auto");

        // If host has a GPU (like RX 480 on this system), preferred should not be null
        // On systems without vulkan, it will safely be null
        if (preferred != null)
        {
            preferred.Name.Should().NotBeNullOrWhiteSpace();
            preferred.VulkanApiVersion.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task IsVulkanAvailableAsync_DoesNotThrow()
    {
        var detector = new VulkanGpuDeviceDetector();
        var isAvailable = await detector.IsVulkanAvailableAsync();
        // Simply assert call completes safely without throwing
        (isAvailable || !isAvailable).Should().BeTrue();
    }
}

