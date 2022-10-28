import React from "react";
import { Breadcrumbs } from "../../atoms/Breadcrumbs/Breadcrumbs";
import { InlineCTA } from "../../molecules/CTA/InlineCTA";
import { Divider } from "../../atoms/Divider/Divider";
import { InputDropdownMachine } from "../../atoms/InputDropdown/InputDropdownMachine";
import Layout from "../../organisms/Layout/Layout";
import { MachineSolutionList } from "../../molecules/MachineSolution/MachineSolutionList";
import { PageHeader } from "../../atoms/PageHeader/PageHeader";
import { getCurrentLanguage } from "../../../features/user/userSlice";
import { useAppSelector } from "../../../utils/hooks";
import { getUser } from "../../../features/auth/authSlice";
import { Navigate } from "react-router-dom";

var translations = require("../../../translations/knowledgebaseTranslations.json");

export function Knowledgebase() {
    const language = useAppSelector(getCurrentLanguage);
    const user = useAppSelector(getUser);
    const userRole = user?.role;

    if (!user) {
        return <Navigate to='/login' />;
    }

    return (
        <>
            <div className='flex flex-col md:flex-row md:h-screen dark:bg-dark-800 dark:text-white overflow-x-hidden'>
                <Layout />
                {/* Sidebar */}
                <div className='flex flex-col gap-4 lg:gap-6 px-6 pt-6 lg:p-8 md:border-r border-gray-200 dark:border-dark-600'>
                    <PageHeader
                        title={translations[language].knowledgebase}
                        subtitle={translations[language].knowledgebase_subtitle}
                    />
                    <Divider />
                    <InputDropdownMachine label={translations[language].search_machine} />
                    {userRole !== "VisconAdmin" && userRole !== "VisconEmployee" ? (
                        <InlineCTA
                            title={translations[language].cant_find_solution_title}
                            text={translations[language].cant_find_solution_text}
                            url='/knowledgebase/create-ticket'
                            button_text={translations[language].create_ticket}
                            button_size='medium'
                            button_type='primary'
                        />
                    ) : undefined}
                </div>
                {/* Solutions */}
                <div className='flex flex-col w-full gap-6 px-6 pb-6 md:pt-6 lg:p-8'>
                    <div className=''>
                        <Breadcrumbs crumbs={[translations[language].knowledgebase]} />
                    </div>
                    <Divider />
                    <MachineSolutionList />
                </div>
            </div>
        </>
    );
}
