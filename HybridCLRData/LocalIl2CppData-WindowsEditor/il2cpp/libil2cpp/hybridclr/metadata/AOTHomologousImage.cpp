#include "AOTHomologousImage.h"

#include "vm/MetadataLock.h"
#include "vm/GlobalMetadata.h"
#include "vm/Class.h"
#include "vm/Image.h"
#include "vm/Exception.h"
#include "vm/MetadataCache.h"
#include "metadata/GenericMetadata.h"

namespace hybridclr
{
namespace metadata
{
	std::vector<AOTHomologousImage*> s_images;


	AOTHomologousImage* AOTHomologousImage::FindImageByAssembly(const Il2CppAssembly* ass)
	{
		il2cpp::os::FastAutoLock lock(&il2cpp::vm::g_MetadataLock);
		return FindImageByAssemblyLocked(ass, lock);
	}

	void AOTHomologousImage::RegisterLocked(AOTHomologousImage* image, il2cpp::os::FastAutoLock& lock)
	{
		IL2CPP_ASSERT(FindImageByAssemblyLocked(image->_targetAssembly, lock) == nullptr);
		s_images.push_back(image);
	}

	AOTHomologousImage* AOTHomologousImage::FindImageByAssemblyLocked(const Il2CppAssembly* ass, il2cpp::os::FastAutoLock& lock)
	{
		for (AOTHomologousImage* image : s_images)
		{
			if (image->_targetAssembly == ass)
			{
				return image;
			}
		}
		return nullptr;
	}

	LoadImageErrorCode AOTHomologousImage::Load(const byte* imageData, size_t length)
	{
		_rawImage = new RawImage();
		LoadImageErrorCode err = _rawImage->Load(imageData, length);
		if (err != LoadImageErrorCode::OK)
		{
			return err;
		}

		TbAssembly data = _rawImage->ReadAssembly(1);
		const char* assName = _rawImage->GetStringFromRawIndex(data.name);
		const Il2CppAssembly* aotAss = GetLoadedAssembly(assName);
		// FIXME. not free memory.
		if (!aotAss)
		{
			return LoadImageErrorCode::AOT_ASSEMBLY_NOT_FIND;
		}
		if (hybridclr::metadata::IsInterpreterImage(aotAss->image))
		{
			return LoadImageErrorCode::HOMOLOGOUS_ONLY_SUPPORT_AOT_ASSEMBLY;
		}
		_targetAssembly = aotAss;

		return LoadImageErrorCode::OK;
	}

	const Il2CppType* AOTHomologousImage::GetModuleIl2CppType(uint32_t moduleRowIndex, uint32_t typeNamespace, uint32_t typeName, bool raiseExceptionIfNotFound)
	{
		IL2CPP_ASSERT(moduleRowIndex == 1);
		const char* typeNameStr = _rawImage->GetStringFromRawIndex(typeName);
		const char* typeNamespaceStr = _rawImage->GetStringFromRawIndex(typeNamespace);

		const Il2CppImage* aotImage = il2cpp::vm::Assembly::GetImage(_targetAssembly);
		Il2CppClass* klass = il2cpp::vm::Class::FromName(aotImage, typeNamespaceStr, typeNameStr);
		if (klass)
		{
			return &klass->byval_arg;
		}
		if (!raiseExceptionIfNotFound)
		{
			return nullptr;
		}
		il2cpp::vm::Exception::Raise(il2cpp::vm::Exception::GetTypeLoadException(
			CStringToStringView(typeNamespaceStr),
			CStringToStringView(typeNameStr),
			CStringToStringView(aotImage->nameNoExt)));
		return nullptr;
	}
}
}

