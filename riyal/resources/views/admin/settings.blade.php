@extends('admin.layout')

@php
    $fields = [
        'Site' => [
            'site_name_ar' => 'Site name (Arabic)',
            'site_name_en' => 'Site name (English)',
            'contact_email' => 'Contact email',
        ],
        'AdSense' => [
            'adsense_client_id' => 'Client / Publisher ID (ca-pub-…)',
            'adsense_slot_in_article' => 'Slot: in-article (after intro)',
            'adsense_slot_mid_article' => 'Slot: mid-article',
            'adsense_slot_below_post' => 'Slot: below post',
            'adsense_slot_sidebar' => 'Slot: sidebar',
            'ads_txt_line' => 'ads.txt line (overrides auto)',
        ],
        'Analytics' => [
            'analytics_id' => 'Google Analytics / GA4 ID (G-…)',
        ],
        'E-E-A-T disclosure' => [
            'ai_disclosure_ar' => 'AI disclosure (Arabic)',
            'ai_disclosure_en' => 'AI disclosure (English)',
        ],
    ];
@endphp

@section('admin')
<h1 class="text-2xl font-bold">Settings</h1>
<p class="mt-1 text-sm text-gray-500">Ad codes and analytics IDs are read from here at render time — change them without a redeploy.</p>

<form action="{{ route('admin.settings.update') }}" method="post" class="mt-6 max-w-2xl space-y-8">
    @csrf @method('PUT')
    @foreach ($fields as $group => $groupFields)
        <fieldset class="rounded-xl border border-gray-200 bg-white p-5">
            <legend class="px-1 text-sm font-semibold text-gray-600">{{ $group }}</legend>
            <div class="space-y-3">
                @foreach ($groupFields as $key => $label)
                    <div>
                        <label class="mb-1 block text-sm font-medium">{{ $label }}</label>
                        @if (str_starts_with($key, 'ai_disclosure'))
                            <textarea name="{{ $key }}" rows="2" class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm">{{ $settings[$key] }}</textarea>
                        @else
                            <input name="{{ $key }}" value="{{ $settings[$key] }}" class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm">
                        @endif
                    </div>
                @endforeach
            </div>
        </fieldset>
    @endforeach

    <button class="rounded-lg bg-brand-600 px-6 py-2.5 font-medium text-white hover:bg-brand-700">Save settings</button>
</form>
@endsection
